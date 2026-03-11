import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import styles from './Layout.module.css';

export function Layout({ children }: { children: React.ReactNode }) {
  const { profile, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <div className={styles.wrapper}>
      <header className={styles.header}>
        <Link to="/" className={styles.logo}>
          SocNet
        </Link>
        <nav className={styles.nav}>
          <NavLink to="/" end className={({ isActive }) => (isActive ? styles.active : '')}>
            Лента
          </NavLink>
          <NavLink to="/favorites" className={({ isActive }) => (isActive ? styles.active : '')}>
            Избранное
          </NavLink>
          <NavLink to="/chats" className={({ isActive }) => (isActive ? styles.active : '')}>
            Чаты
          </NavLink>
          <NavLink to="/search" className={({ isActive }) => (isActive ? styles.active : '')}>
            Поиск
          </NavLink>
          {profile?.is_admin && (
            <NavLink to="/admin" className={({ isActive }) => (isActive ? styles.active : '')}>
              Админка
            </NavLink>
          )}
          {profile && (
            <NavLink
              to={`/user/${profile.id}`}
              className={({ isActive }) => (isActive ? styles.active : '')}
            >
              Профиль
            </NavLink>
          )}
        </nav>
        <div className={styles.user}>
          {profile ? (
            <>
              <span className={styles.nick}>@{profile.nick}</span>
              <button type="button" onClick={handleLogout} className={styles.logout}>
                Выход
              </button>
            </>
          ) : (
            <Link to="/login">Войти</Link>
          )}
        </div>
      </header>
      <main className={styles.main}>{children}</main>
    </div>
  );
}
