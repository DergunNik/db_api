import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import * as authApi from '../api/auth';
import type { UserProfile } from '../api/users';
import { getProfile } from '../api/users';

interface AuthState {
  token: string | null;
  profile: UserProfile | null;
  loading: boolean;
}

interface AuthContextValue extends AuthState {
  login: (nick: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('token'));
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshProfile = useCallback(async () => {
    if (!token) {
      setProfile(null);
      return;
    }
    try {
      const p = await getProfile();
      setProfile(p);
    } catch {
      setToken(null);
      localStorage.removeItem('token');
      setProfile(null);
    }
  }, [token]);

  useEffect(() => {
    if (!token) {
      setLoading(false);
      setProfile(null);
      return;
    }
    refreshProfile().finally(() => setLoading(false));
  }, [token, refreshProfile]);

  const login = useCallback(async (nick: string, password: string) => {
    const { token: t } = await authApi.login(nick, password);
    localStorage.setItem('token', t);
    setToken(t);
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {}
    localStorage.removeItem('token');
    setToken(null);
    setProfile(null);
  }, []);

  const value: AuthContextValue = {
    token,
    profile,
    loading,
    login,
    logout,
    refreshProfile,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
