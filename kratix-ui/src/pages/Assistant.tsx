import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  Add as AddIcon,
  DeleteOutlined as DeleteIcon,
  Description as FileIcon,
  Download as DownloadIcon,
  Forum as ForumIcon,
  Insights as InsightsIcon,
  Send as SendIcon,
  Slideshow as SlidesIcon,
  ThumbDownOutlined as ThumbDownIcon,
  ThumbDownAlt as ThumbDownFilledIcon,
  ThumbUpOutlined as ThumbUpIcon,
  ThumbUpAlt as ThumbUpFilledIcon,
} from '@mui/icons-material';
import { agentApi, ask, type AgentReport, type AgentTable, type ChatSession } from '../api/agent';
import { useAuth } from '../hooks/useAuth';
import { useToast } from '../hooks/useToast';
import { canSeeOperationsReports, canSeeSalesReports } from '../auth/permissions';
import { formatDateTime } from '../utils/format';
import ConfirmDialog from '../components/ui/ConfirmDialog';
import AnswerTable from '../components/assistant/AnswerTable';
import ReportDialog from '../components/assistant/ReportDialog';
import UsageDialog from '../components/assistant/UsageDialog';

interface UiMessage {
  key: string;
  role: 'user' | 'assistant';
  content: string;
  id?: string;                       // the server's message id, once the answer is saved
  status?: string;                   // "Checking sales..." while the ERP is read
  tables: AgentTable[];
  report?: Pick<AgentReport, 'id'> & Partial<AgentReport>;
  error?: string;
  streaming?: boolean;
  rating?: 1 | -1;
}

const SESSION_PANE = 272;
let keySeed = 0;
const nextKey = () => `m${++keySeed}`;

// Starter questions follow what each role can see in the ERP.
function suggestionsFor(role: string | undefined): string[] {
  const list: string[] = [];
  if (canSeeSalesReports(role)) list.push('Which customers owe us money?', "Give me this month's sales summary.");
  if (canSeeOperationsReports(role)) list.push('How much wheat do we have?', 'What was our milling yield this month?');
  if (role === 'Owner' || role === 'Admin') list.push("Make this month's sales report in PowerPoint.");
  else if (canSeeOperationsReports(role)) list.push("Make this month's production report in Excel.");
  if (canSeeSalesReports(role) && list.length < 6) list.push('Is mahine kitni sale hui?');
  return list;
}

const ReportCard: React.FC<{ report: UiMessage['report'] }> = ({ report }) => {
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  if (!report) return null;
  const isSlides = report.format === 'pptx' || report.fileName?.endsWith('.pptx');
  const download = async () => {
    setBusy(true);
    try {
      await agentApi.download(report.id, report.fileName);
    } catch (err) {
      toast(err instanceof Error && 'status' in err && err.status === 404
        ? 'This report has expired. Ask for it again.' : 'The report could not be downloaded.', 'error');
    } finally {
      setBusy(false);
    }
  };
  return (
    <Paper variant="outlined" sx={{ mt: 1.5, p: 1.25, pl: 1.5, borderRadius: '8px', display: 'flex', alignItems: 'center', gap: 1.5 }}>
      {isSlides ? <SlidesIcon sx={{ color: 'var(--accent-text)' }} /> : <FileIcon sx={{ color: 'var(--accent-text)' }} />}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Typography sx={{ fontSize: '13px', fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {report.fileName ?? 'Report'}
        </Typography>
        <Typography sx={{ fontSize: '12px', color: 'text.secondary' }}>
          {isSlides ? 'PowerPoint' : 'Excel'}{report.sizeBytes ? ` · ${Math.max(1, Math.round(report.sizeBytes / 1024))} KB` : ''} · kept 7 days
        </Typography>
      </Box>
      <Button size="small" variant="outlined" startIcon={busy ? <CircularProgress size={14} /> : <DownloadIcon />} onClick={download} disabled={busy}>
        Download
      </Button>
    </Paper>
  );
};

const Assistant: React.FC = () => {
  const { user } = useAuth();
  const toast = useToast();
  const [sessions, setSessions] = useState<ChatSession[] | null>(null);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<UiMessage[]>([]);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [offline, setOffline] = useState('');
  const [sessionsOpen, setSessionsOpen] = useState(false);
  const [deleting, setDeleting] = useState<ChatSession | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [reportOpen, setReportOpen] = useState(false);
  const [usageOpen, setUsageOpen] = useState(false);
  const [downRating, setDownRating] = useState<UiMessage | null>(null);
  const [downComment, setDownComment] = useState('');
  const abort = useRef<AbortController | null>(null);
  const bottom = useRef<HTMLDivElement | null>(null);
  const isManager = user?.role === 'Owner' || user?.role === 'Admin';

  const showSessions = useCallback((list: ChatSession[]) => {
    setSessions(list);
    setOffline('');
  }, []);
  const showOffline = useCallback((err: unknown) => {
    setSessions((list) => list ?? []);
    setOffline(err instanceof Error ? err.message : 'The assistant is not reachable.');
  }, []);
  const loadSessions = useCallback(() => agentApi.listSessions().then(showSessions, showOffline), [showSessions, showOffline]);

  useEffect(() => {
    let live = true;
    agentApi.listSessions().then((list) => live && showSessions(list), (err) => live && showOffline(err));
    return () => {
      live = false;
      abort.current?.abort();
    };
  }, [showSessions, showOffline]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: 'end' });
  }, [messages]);

  const update = (key: string, change: (m: UiMessage) => UiMessage) =>
    setMessages((list) => list.map((m) => (m.key === key ? change(m) : m)));

  const openSession = async (session: ChatSession) => {
    if (busy || session.id === activeId) return;
    setActiveId(session.id);
    setSessionsOpen(false);
    setLoadingMessages(true);
    try {
      const stored = await agentApi.messages(session.id);
      setMessages(stored.map((m) => ({
        key: nextKey(), role: m.role, content: m.content, id: m.role === 'assistant' ? m.id : undefined, tables: [],
        report: m.reportId ? { id: m.reportId } : undefined,
      })));
    } catch (err) {
      toast(err instanceof Error ? err.message : 'The conversation could not be opened.', 'error');
    } finally {
      setLoadingMessages(false);
    }
  };

  const newChat = () => {
    if (busy) return;
    setActiveId(null);
    setMessages([]);
    setSessionsOpen(false);
  };

  const send = async (text: string) => {
    const question = text.trim();
    if (!question || busy) return;
    setInput('');
    setBusy(true);
    const answerKey = nextKey();
    setMessages((list) => [
      ...list,
      { key: nextKey(), role: 'user', content: question, tables: [] },
      { key: answerKey, role: 'assistant', content: '', tables: [], streaming: true, status: 'Thinking...' },
    ]);
    const controller = new AbortController();
    abort.current = controller;
    try {
      let sessionId = activeId;
      if (!sessionId) {
        sessionId = (await agentApi.createSession()).id;
        setActiveId(sessionId);
      }
      await ask(sessionId, question, {
        onTool: (label) => update(answerKey, (m) => ({ ...m, status: label })),
        onToken: (t) => update(answerKey, (m) => ({ ...m, status: undefined, content: m.content + t })),
        onData: (table) => update(answerKey, (m) => ({ ...m, tables: [...m.tables, table] })),
        onReport: (report) => update(answerKey, (m) => ({ ...m, report })),
        onDone: (done) => update(answerKey, (m) => ({ ...m, id: done.messageId, streaming: false, status: undefined })),
        onError: (message) => update(answerKey, (m) => ({ ...m, error: message, streaming: false, status: undefined })),
      }, controller.signal);
      setOffline('');
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      update(answerKey, (m) => ({ ...m, streaming: false, status: undefined, error: err instanceof Error ? err.message : 'Something went wrong.' }));
    } finally {
      update(answerKey, (m) => ({ ...m, streaming: false, status: undefined }));
      setBusy(false);
      abort.current = null;
      void loadSessions();
    }
  };

  const rate = async (message: UiMessage, rating: 1 | -1, comment?: string) => {
    if (!message.id) return;
    try {
      await agentApi.feedback(message.id, rating, comment);
      update(message.key, (m) => ({ ...m, rating }));
      if (rating === -1) toast('Thanks. The owner will see this answer in the usage review.', 'success');
    } catch (err) {
      toast(err instanceof Error ? err.message : 'Feedback could not be saved.', 'error');
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await agentApi.deleteSession(deleting.id);
      if (deleting.id === activeId) newChat();
      await loadSessions();
    } catch (err) {
      toast(err instanceof Error ? err.message : 'The conversation could not be deleted.', 'error');
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  };

  const sessionList = (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <Box sx={{ p: 1.5 }}>
        <Button fullWidth variant="contained" startIcon={<AddIcon />} onClick={newChat} disabled={busy}>New conversation</Button>
      </Box>
      <Box sx={{ flex: 1, overflowY: 'auto', px: 1, pb: 1 }}>
        {sessions === null && <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress size={22} /></Box>}
        {sessions?.length === 0 && (
          <Typography sx={{ fontSize: '12px', color: 'text.secondary', px: 1, py: 2 }}>Your conversations will be listed here.</Typography>
        )}
        <List disablePadding>
          {sessions?.map((s) => (
            <ListItemButton
              key={s.id}
              selected={s.id === activeId}
              onClick={() => openSession(s)}
              sx={{ borderRadius: '8px', mb: 0.25, pr: 0.5, '&.Mui-selected': { bgcolor: 'color-mix(in srgb, var(--mui-palette-primary-main) 18%, transparent)' }, '&:hover .delete': { opacity: 1 } }}
            >
              <ListItemText
                primary={s.title || 'New conversation'}
                secondary={formatDateTime(s.updatedAt ?? s.createdAt)}
                slotProps={{
                  primary: { sx: { fontSize: '13px', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } },
                  secondary: { sx: { fontSize: '11px' } },
                }}
              />
              <IconButton
                className="delete"
                size="small"
                aria-label={`Delete ${s.title || 'conversation'}`}
                onClick={(e) => { e.stopPropagation(); setDeleting(s); }}
                sx={{ opacity: { xs: 1, md: 0 }, transition: 'opacity 0.15s' }}
              >
                <DeleteIcon sx={{ fontSize: 18 }} />
              </IconButton>
            </ListItemButton>
          ))}
        </List>
      </Box>
    </Box>
  );

  const suggestions = suggestionsFor(user?.role);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100dvh - 64px - 48px)', minHeight: 480 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2, flexWrap: 'wrap' }}>
        <IconButton aria-label="Conversations" onClick={() => setSessionsOpen(true)} sx={{ display: { md: 'none' } }}>
          <ForumIcon />
        </IconButton>
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <Typography sx={{ fontSize: '28px', fontWeight: 700, letterSpacing: '-0.5px', lineHeight: 1.2 }}>Assistant</Typography>
          <Typography sx={{ fontSize: '14px', color: 'text.secondary' }}>
            Ask about sales, stock, production or suppliers in English or Urdu. Answers come from live ERP data you are allowed to see.
          </Typography>
        </Box>
        <Button variant="outlined" startIcon={<FileIcon />} onClick={() => setReportOpen(true)}>Make a report</Button>
        {isManager && <Button variant="outlined" startIcon={<InsightsIcon />} onClick={() => setUsageOpen(true)}>Usage</Button>}
      </Box>

      {offline && <Alert severity="warning" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={loadSessions}>Retry</Button>}>{offline}</Alert>}

      <Paper variant="outlined" sx={{ flex: 1, minHeight: 0, display: 'flex', borderRadius: '12px', overflow: 'hidden' }}>
        <Box sx={{ width: SESSION_PANE, flexShrink: 0, borderRight: 1, borderColor: 'divider', display: { xs: 'none', md: 'block' } }}>
          {sessionList}
        </Box>
        <Drawer open={sessionsOpen} onClose={() => setSessionsOpen(false)} sx={{ display: { md: 'none' }, '& .MuiDrawer-paper': { width: SESSION_PANE } }}>
          {sessionList}
        </Drawer>

        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
          <Box sx={{ flex: 1, overflowY: 'auto', px: { xs: 2, md: 4 }, py: 3 }}>
            {loadingMessages && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}

            {!loadingMessages && messages.length === 0 && (
              <Box sx={{ maxWidth: 640, mx: 'auto', mt: { xs: 2, md: 8 }, textAlign: 'center' }}>
                <Typography sx={{ fontSize: '20px', fontWeight: 700 }}>What would you like to know?</Typography>
                <Typography sx={{ fontSize: '13px', color: 'text.secondary', mt: 0.5, mb: 3 }}>
                  Try one of these, or ask for a report as Excel or PowerPoint.
                </Typography>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, justifyContent: 'center' }}>
                  {suggestions.map((s) => (
                    <Chip key={s} label={s} onClick={() => send(s)} variant="outlined" disabled={busy}
                      sx={{ fontSize: '13px', height: 34, borderRadius: '8px', '&:hover': { borderColor: 'var(--accent-border)' } }} />
                  ))}
                </Box>
              </Box>
            )}

            {!loadingMessages && messages.map((m) => (m.role === 'user' ? (
              <Box key={m.key} sx={{ display: 'flex', justifyContent: 'flex-end', mb: 2 }}>
                <Box sx={{ maxWidth: '75%', px: 1.75, py: 1.1, borderRadius: '12px 12px 4px 12px', bgcolor: 'color-mix(in srgb, var(--mui-palette-primary-main) 22%, transparent)' }}>
                  <Typography sx={{ fontSize: '14px', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{m.content}</Typography>
                </Box>
              </Box>
            ) : (
              <Box key={m.key} sx={{ mb: 3, maxWidth: 920 }}>
                <Typography sx={{ fontSize: '11px', fontWeight: 800, letterSpacing: '0.8px', color: 'var(--accent-text)', mb: 0.5 }}>KRATIX</Typography>
                {m.status && (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: 'text.secondary' }} role="status">
                    <CircularProgress size={14} />
                    <Typography sx={{ fontSize: '13px' }}>{m.status}</Typography>
                  </Box>
                )}
                {m.content && (
                  <Typography sx={{ fontSize: '14px', lineHeight: 1.6, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                    {m.content}
                    {m.streaming && <Box component="span" sx={{ display: 'inline-block', width: 7, height: 15, ml: 0.25, bgcolor: 'text.secondary', verticalAlign: 'text-bottom', animation: 'blink 1s steps(2) infinite', '@keyframes blink': { '50%': { opacity: 0 } } }} />}
                  </Typography>
                )}
                {m.error && <Alert severity="error" sx={{ mt: 1 }}>{m.error}</Alert>}
                <ReportCard report={m.report} />
                {m.tables.map((t, i) => <AnswerTable key={i} table={t} />)}
                {m.id && !m.streaming && (
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 0.75, color: 'text.secondary' }}>
                    <Tooltip title="Good answer">
                      <IconButton size="small" aria-label="Good answer" onClick={() => rate(m, 1)} color={m.rating === 1 ? 'success' : 'default'}>
                        {m.rating === 1 ? <ThumbUpFilledIcon sx={{ fontSize: 16 }} /> : <ThumbUpIcon sx={{ fontSize: 16 }} />}
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Wrong or unhelpful">
                      <IconButton size="small" aria-label="Wrong or unhelpful" onClick={() => { setDownComment(''); setDownRating(m); }} color={m.rating === -1 ? 'error' : 'default'}>
                        {m.rating === -1 ? <ThumbDownFilledIcon sx={{ fontSize: 16 }} /> : <ThumbDownIcon sx={{ fontSize: 16 }} />}
                      </IconButton>
                    </Tooltip>
                  </Box>
                )}
              </Box>
            )))}
            <div ref={bottom} />
          </Box>

          <Box
            component="form"
            onSubmit={(e) => { e.preventDefault(); void send(input); }}
            sx={{ borderTop: 1, borderColor: 'divider', p: 1.5, display: 'flex', gap: 1, alignItems: 'flex-end' }}
          >
            <TextField
              fullWidth
              multiline
              maxRows={5}
              placeholder="Ask a question, e.g. Has PO-2026-001 been fully received?"
              value={input}
              onChange={(e) => setInput(e.target.value.slice(0, 1000))}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault();
                  void send(input);
                }
              }}
              slotProps={{ htmlInput: { 'aria-label': 'Question' } }}
            />
            <Button type="submit" variant="contained" disabled={busy || !input.trim()} sx={{ minWidth: 48, height: 40 }} aria-label="Send">
              {busy ? <CircularProgress size={18} color="inherit" /> : <SendIcon fontSize="small" />}
            </Button>
          </Box>
        </Box>
      </Paper>

      <ConfirmDialog
        open={!!deleting}
        title="Delete conversation"
        message={`"${deleting?.title || 'New conversation'}" will be removed from your list.`}
        confirmLabel="Delete"
        busy={deleteBusy}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />

      <Dialog open={!!downRating} onClose={() => setDownRating(null)} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 700 }}>What was wrong?</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={2}
            placeholder="Optional, e.g. the total should be PKR 311,000"
            value={downComment}
            onChange={(e) => setDownComment(e.target.value.slice(0, 1000))}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setDownRating(null)}>Cancel</Button>
          <Button variant="contained" onClick={() => { if (downRating) void rate(downRating, -1, downComment.trim()); setDownRating(null); }}>
            Send
          </Button>
        </DialogActions>
      </Dialog>

      <ReportDialog
        open={reportOpen}
        role={user?.role}
        onClose={() => setReportOpen(false)}
        onCreated={(r) => toast(`${r.fileName} downloaded.`, 'success')}
      />
      {isManager && <UsageDialog open={usageOpen} onClose={() => setUsageOpen(false)} />}
    </Box>
  );
};

export default Assistant;
