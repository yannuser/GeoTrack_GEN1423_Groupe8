import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MAINTENANT, VEHICULES } from '../test/fixtures';
import { PanneauVehicules } from './PanneauVehicules';

function afficherPanneau(vehiculeSelectionne: string | null = null) {
  const onSelection = vi.fn();
  render(
    <PanneauVehicules
      vehicules={VEHICULES}
      vehiculeSelectionne={vehiculeSelectionne}
      onSelection={onSelection}
      maintenant={MAINTENANT}
    />,
  );
  return { onSelection };
}

/** Une entree de liste = un bouton, dont le nom accessible concatene id, statut, vitesse. */
function lignesVehicules() {
  return within(screen.getByRole('list')).getAllByRole('button');
}

describe('PanneauVehicules', () => {
  it('affiche une ligne par vehicule et le compteur correspondant', () => {
    afficherPanneau();

    expect(lignesVehicules()).toHaveLength(VEHICULES.length);
    expect(screen.getByText('VEHICULES ACTIFS')).toBeInTheDocument();
    expect(screen.getByText(String(VEHICULES.length))).toBeInTheDocument();
  });

  it('affiche l identifiant de chaque vehicule', () => {
    afficherPanneau();

    for (const vehicule of VEHICULES) {
      expect(screen.getByText(vehicule.vehiculeId)).toBeInTheDocument();
    }
  });

  it('affiche la vitesse arrondie d un vehicule en route', () => {
    afficherPanneau();

    const ligne = lignesVehicules()[VEHICULES.findIndex((v) => v.vehiculeId === 'VH-001')];

    // Fixture : 62.4 km/h -> arrondi a 62.
    expect(within(ligne).getByText('62')).toBeInTheDocument();
    expect(ligne).toHaveTextContent('62 km/h');
  });

  it('remplace la vitesse par un tiret pour un vehicule immobile', () => {
    afficherPanneau();

    for (const vehiculeId of ['VH-002', 'VH-003']) {
      const ligne = lignesVehicules()[VEHICULES.findIndex((v) => v.vehiculeId === vehiculeId)];
      expect(ligne).not.toHaveTextContent('km/h');
      expect(ligne).toHaveTextContent('—');
    }
  });

  it('associe a chaque vehicule son libelle de statut', () => {
    afficherPanneau();

    const parId = new Map(
      lignesVehicules().map((ligne, index) => [VEHICULES[index].vehiculeId, ligne]),
    );

    expect(parId.get('VH-001')).toHaveTextContent('En route');
    expect(parId.get('VH-002')).toHaveTextContent("A l'arret");
    expect(parId.get('VH-003')).toHaveTextContent('Panne');
  });

  it('affiche un message dedie quand aucun vehicule ne passe les filtres', () => {
    render(
      <PanneauVehicules
        vehicules={[]}
        vehiculeSelectionne={null}
        onSelection={vi.fn()}
        maintenant={MAINTENANT}
      />,
    );

    expect(screen.getByText(/Aucun vehicule ne correspond aux filtres/i)).toBeInTheDocument();
    expect(within(screen.getByRole('list')).queryAllByRole('button')).toHaveLength(0);
  });

  it('marque le vehicule selectionne et remonte les clics', () => {
    const { onSelection } = afficherPanneau('VH-003');

    const lignes = lignesVehicules();
    const indexSelectionne = VEHICULES.findIndex((v) => v.vehiculeId === 'VH-003');
    expect(lignes[indexSelectionne]).toHaveAttribute('aria-pressed', 'true');
    expect(lignes[0]).toHaveAttribute('aria-pressed', 'false');

    fireEvent.click(lignes[0]);
    expect(onSelection).toHaveBeenCalledExactlyOnceWith(VEHICULES[0].vehiculeId);
  });
});
