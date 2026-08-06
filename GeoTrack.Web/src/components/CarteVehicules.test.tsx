import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MAINTENANT, VEHICULES } from '../test/fixtures';
import { STATUTS } from '../types';
import { CarteVehicules } from './CarteVehicules';

vi.mock('react-leaflet', () => import('../test/leafletMock'));

function afficherCarte(vehiculeSelectionne: string | null = null) {
  const onSelection = vi.fn();
  render(
    <CarteVehicules
      vehicules={VEHICULES}
      vehiculeSelectionne={vehiculeSelectionne}
      onSelection={onSelection}
      maintenant={MAINTENANT}
    />,
  );
  return { onSelection };
}

describe('CarteVehicules', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('affiche un marqueur par vehicule', () => {
    afficherCarte();

    const marqueurs = screen.getAllByTestId('marqueur');
    expect(marqueurs).toHaveLength(VEHICULES.length);
    expect(marqueurs).toHaveLength(3);
  });

  it('positionne chaque marqueur sur les coordonnees de son vehicule', () => {
    afficherCarte();

    const coordonnees = screen
      .getAllByTestId('marqueur')
      .map((noeud) => [noeud.dataset.latitude, noeud.dataset.longitude]);

    expect(coordonnees).toEqual(
      VEHICULES.map((vehicule) => [String(vehicule.latitude), String(vehicule.longitude)]),
    );
  });

  it('n affiche aucun marqueur quand la liste est vide', () => {
    render(
      <CarteVehicules
        vehicules={[]}
        vehiculeSelectionne={null}
        onSelection={vi.fn()}
        maintenant={MAINTENANT}
      />,
    );

    expect(screen.queryAllByTestId('marqueur')).toHaveLength(0);
    expect(screen.getByTestId('carte')).toBeInTheDocument();
  });

  describe('couleur du marqueur selon le statut', () => {
    it.each([
      ['VH-001', 'en_route', '#22a05b'],
      ['VH-002', 'a_l_arret', '#8a8f98'],
      ['VH-003', 'panne', '#e03131'],
    ] as const)('%s (%s) est colore en %s', (vehiculeId, statut, couleurAttendue) => {
      afficherCarte();

      const index = VEHICULES.findIndex((vehicule) => vehicule.vehiculeId === vehiculeId);
      const marqueur = screen.getAllByTestId('marqueur')[index];

      expect(VEHICULES[index].statut).toBe(statut);
      expect(marqueur.dataset.couleur).toBe(couleurAttendue);
      // La couleur affichee vient bien de la table de reference des statuts.
      expect(marqueur.dataset.couleur).toBe(STATUTS[statut].couleur);
    });
  });

  it('agrandit le marqueur du vehicule selectionne', () => {
    afficherCarte('VH-002');

    const marqueurs = screen.getAllByTestId('marqueur');
    const indexSelectionne = VEHICULES.findIndex((v) => v.vehiculeId === 'VH-002');

    expect(marqueurs[indexSelectionne].dataset.rayon).toBe('11');
    marqueurs
      .filter((_, index) => index !== indexSelectionne)
      .forEach((marqueur) => expect(marqueur.dataset.rayon).toBe('8'));
  });

  it('remonte le vehicule au clic sur son marqueur', () => {
    const { onSelection } = afficherCarte();

    fireEvent.click(screen.getAllByTestId('marqueur')[0]);

    expect(onSelection).toHaveBeenCalledExactlyOnceWith(VEHICULES[0].vehiculeId);
  });
});
