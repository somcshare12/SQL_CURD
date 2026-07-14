import { BrowserRouter } from 'react-router-dom';
import { AppProviders } from './AppProviders';
import { AppRouter } from './AppRouter';
import { AppLayout } from './AppLayout';

/**
 * The standalone application root. It demonstrates how a host composes the
 * module: providers → router → layout. An embedded parent would instead render
 * <SqlDataManagerPage /> (or the module routes) inside its own shell.
 */
export function App() {
  return (
    <AppProviders>
      <BrowserRouter>
        <AppLayout>
          <AppRouter />
        </AppLayout>
      </BrowserRouter>
    </AppProviders>
  );
}
