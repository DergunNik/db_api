import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error) {
    console.error('App error:', error);
  }

  render() {
    if (this.state.hasError && this.state.error) {
      return (
        <div
          style={{
            minHeight: '100vh',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '2rem',
            background: '#1a1b26',
            color: '#c0caf5',
            fontFamily: 'system-ui, sans-serif',
          }}
        >
          <h1 style={{ color: '#f7768e' }}>Ошибка</h1>
          <pre
            style={{
              background: '#24283b',
              padding: '1rem',
              borderRadius: 8,
              overflow: 'auto',
              maxWidth: '100%',
              fontSize: '0.9rem',
            }}
          >
            {this.state.error.message}
          </pre>
          <button
            type="button"
            onClick={() => this.setState({ hasError: false })}
            style={{
              marginTop: '1rem',
              padding: '0.5rem 1rem',
              background: '#7aa2f7',
              border: 'none',
              borderRadius: 8,
              cursor: 'pointer',
              color: '#1a1b26',
            }}
          >
            Повторить
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
