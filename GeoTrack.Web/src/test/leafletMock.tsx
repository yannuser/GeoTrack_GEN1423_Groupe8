import type { ReactNode } from 'react';
import { vi } from 'vitest';

/**
 * Double de test de `react-leaflet`.
 *
 * Leaflet a besoin d'un vrai moteur de rendu (dimensions, SVG, tuiles reseau)
 * que jsdom ne fournit pas. On remplace donc les composants par des elements
 * DOM simples qui exposent, via des attributs `data-*`, exactement ce que les
 * tests doivent verifier : la presence d'un marqueur par vehicule, ses
 * coordonnees et sa couleur de remplissage.
 *
 * Usage : vi.mock('react-leaflet', () => import('<chemin>/test/leafletMock'));
 */

interface OptionsTrace {
  color?: string;
  weight?: number;
  fillColor?: string;
  fillOpacity?: number;
}

/** Instance de carte factice renvoyee par `useMap()`. */
export const carteFactice = {
  fitBounds: vi.fn(),
  flyTo: vi.fn(),
  getZoom: vi.fn(() => 12),
  setView: vi.fn(),
};

export function reinitialiserCarteFactice() {
  carteFactice.fitBounds.mockClear();
  carteFactice.flyTo.mockClear();
  carteFactice.getZoom.mockClear();
  carteFactice.setView.mockClear();
}

export function useMap() {
  return carteFactice;
}

export function MapContainer({ children }: { children?: ReactNode }) {
  return <div data-testid="carte">{children}</div>;
}

export function TileLayer() {
  return <div data-testid="tuiles" />;
}

export function CircleMarker({
  center,
  radius,
  pathOptions,
  eventHandlers,
  children,
}: {
  center: [number, number];
  radius?: number;
  pathOptions?: OptionsTrace;
  eventHandlers?: { click?: () => void };
  children?: ReactNode;
}) {
  return (
    <div
      data-testid="marqueur"
      data-latitude={center[0]}
      data-longitude={center[1]}
      data-rayon={radius}
      data-couleur={pathOptions?.fillColor}
      onClick={() => eventHandlers?.click?.()}
    >
      {children}
    </div>
  );
}

export function Tooltip({ children }: { children?: ReactNode }) {
  return <div data-testid="infobulle">{children}</div>;
}

export function Popup({ children }: { children?: ReactNode }) {
  return <div data-testid="popup">{children}</div>;
}
