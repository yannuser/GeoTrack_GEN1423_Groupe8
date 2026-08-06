import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// RTL ne nettoie pas automatiquement quand `globals` est actif sans son propre setup.
afterEach(() => {
  cleanup();
});
