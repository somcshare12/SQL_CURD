// Vitest setup: extends `expect` with Testing Library's DOM matchers and
// ensures the DOM is cleaned up between tests.
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

afterEach(() => cleanup());
