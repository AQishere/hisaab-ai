// Hisaab Assistant (hisaab-agent, FastAPI on :5080). Same sign-in token as the ERP; the agent checks it itself.
import { session } from '../auth/session';

// 127.0.0.1, not localhost: on Windows "localhost" tries IPv6 first, which the agent does not listen on.
const AGENT_URL = (import.meta.env.VITE_AGENT_URL || 'http://127.0.0.1:5080').replace(/\/$/, '');

export interface ChatSession {
  id: string;
  title: string | null;
  createdAt: string;
  updatedAt?: string;
}

export interface StoredMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  reportId: string | null;
  createdAt: string;
}

export interface AgentTable {
  tool: string;
  title: string;
  count: number;
  rows: Record<string, unknown>[];
}

export interface AgentReport {
  id: string;
  type: string;
  format: 'xlsx' | 'pptx';
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  expiresAt: string;
  downloadUrl: string;
}

export interface ToolCall {
  name: string;
  arguments: Record<string, unknown>;
  error: string | null;
}

export interface AnswerDone {
  messageId: string;
  toolCalls: ToolCall[];
  firstTokenMs: number | null;
  totalMs: number;
  report: AgentReport | null;
}

export interface AgentStats {
  days: number;
  questions: number;
  byStatus: Record<string, number>;
  failedPercent: number;
  latencyMs: { firstWords: { p50: number | null; p95: number | null }; complete: { p50: number | null; p95: number | null } };
  perDay: { date: string; questions: number; failed: number }[];
  perUser: { username: string; role: string; questions: number }[];
  tools: { name: string; calls: number; errors: number }[];
  llm: { byProvider: Record<string, number>; fallbackQuestions: number; fallbackPercent: number; promptTokens: number; completionTokens: number; costUsd: number };
  feedback: { up: number; down: number };
  thumbsDown: { messageId: string; username: string | null; question: string | null; answer: string; comment: string | null; createdAt: string }[];
  reports: Record<string, number>;
}

export class AgentError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

const OFFLINE = 'The assistant is not reachable. Check that hisaab-agent is running on port 5080.';

async function request(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  const token = session.token();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  let response: Response;
  try {
    response = await fetch(`${AGENT_URL}${path}`, { ...init, headers });
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new AgentError(0, OFFLINE);
  }
  if (response.ok) return response;
  if (response.status === 401) {
    // Same as the ERP client: an expired or changed account signs in again.
    session.clear();
    window.location.href = '/login';
  }
  let message = `The assistant returned an error (${response.status}).`;
  try {
    const body = (await response.json()) as { detail?: unknown };
    if (typeof body.detail === 'string') message = body.detail;
  } catch {
    // keep the generic message
  }
  throw new AgentError(response.status, message);
}

const json = async <T>(path: string, init?: RequestInit) => (await (await request(path, init)).json()) as T;

export const agentApi = {
  listSessions: () => json<ChatSession[]>('/chat/sessions'),
  createSession: () => json<ChatSession>('/chat/sessions', { method: 'POST' }),
  deleteSession: (id: string) => request(`/chat/sessions/${id}`, { method: 'DELETE' }).then(() => undefined),
  messages: (id: string) => json<StoredMessage[]>(`/chat/sessions/${id}/messages`),
  feedback: (messageId: string, rating: 1 | -1, comment?: string) =>
    json(`/messages/${messageId}/feedback`, { method: 'POST', body: JSON.stringify({ rating, comment: comment || null }) }),
  stats: (days: number) => json<AgentStats>(`/admin/stats?days=${days}`),
  createReport: (body: { type: string; format: string; period?: string; from?: string; to?: string }) =>
    json<AgentReport>('/reports', { method: 'POST', body: JSON.stringify(body) }),

  async download(reportId: string, fileName?: string) {
    const response = await request(`/reports/${reportId}/download`);
    const name = fileName ?? /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(response.headers.get('Content-Disposition') ?? '')?.[1] ?? 'report';
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = decodeURIComponent(name);
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};

export interface AskHandlers {
  onTool?: (label: string) => void;
  onToken: (text: string) => void;
  onData?: (table: AgentTable) => void;
  onReport?: (report: AgentReport) => void;
  onDone?: (done: AnswerDone) => void;
  onError?: (message: string) => void;
}

/** Ask a question and receive the answer as it is written (server-sent events over a POST). */
export async function ask(sessionId: string, question: string, handlers: AskHandlers, signal?: AbortSignal) {
  const response = await request(`/chat/sessions/${sessionId}/ask`, {
    method: 'POST',
    body: JSON.stringify({ question }),
    signal,
  });
  if (!response.body) throw new AgentError(0, OFFLINE);
  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let buffer = '';
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += value;
    let end: number;
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      const event = /^event: (.*)$/m.exec(block)?.[1];
      const data = /^data: (.*)$/m.exec(block)?.[1];
      if (!event || data === undefined) continue;
      const payload = JSON.parse(data);
      if (event === 'token') handlers.onToken(payload.text);
      else if (event === 'tool') handlers.onTool?.(payload.label);
      else if (event === 'data') handlers.onData?.(payload);
      else if (event === 'report') handlers.onReport?.(payload);
      else if (event === 'done') handlers.onDone?.(payload);
      else if (event === 'error') handlers.onError?.(payload.message);
    }
  }
}
