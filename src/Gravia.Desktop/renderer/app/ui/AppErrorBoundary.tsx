import { Component } from 'react';
import type { ErrorInfo, PropsWithChildren, ReactNode } from 'react';
import { Button, Result } from 'antd';
import { useTranslation } from 'react-i18next';

interface BoundaryProps extends PropsWithChildren {
  readonly fallback: ReactNode;
}

interface BoundaryState {
  readonly failed: boolean;
}

class ErrorBoundary extends Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = { failed: false };

  static getDerivedStateFromError(): BoundaryState {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Gravia Lab renderer failed.', error, info.componentStack);
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children;
  }
}

export function AppErrorBoundary({ children }: PropsWithChildren) {
  const { t } = useTranslation();
  const fallback = (
    <Result
      status="error"
      title={t('unexpectedError.title')}
      subTitle={t('unexpectedError.description')}
      extra={
        <Button type="primary" onClick={() => window.location.reload()}>
          {t('unexpectedError.reloadAction')}
        </Button>
      }
    />
  );
  return <ErrorBoundary fallback={fallback}>{children}</ErrorBoundary>;
}
