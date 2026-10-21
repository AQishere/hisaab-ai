import React, { useState, useEffect } from 'react';
import { session } from '../auth/session';

import { AuthContext, type User } from '../hooks/useAuth';

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(() => session.token() ? session.user<User>() : null);

  useEffect(() => {
    if (!session.token() || !session.user<User>()) session.clear();
  }, []);

  const login = (token: string, userData: User, remember: boolean) => {
    session.save(token, userData, remember);
    setUser(userData);
  };

  const logout = () => {
    session.clear();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, isLoading: false, login, logout, isAuthenticated: !!user }}>
      {children}
    </AuthContext.Provider>
  );
};
