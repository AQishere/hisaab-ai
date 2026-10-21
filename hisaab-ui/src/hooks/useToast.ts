import { createContext, useContext } from 'react';

export type Severity = 'success' | 'error' | 'info';
export const ToastContext = createContext<(message: string, severity?: Severity) => void>(() => {});
export const useToast = () => useContext(ToastContext);
