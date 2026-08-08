import { act, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ALERTES_API, reponseJson } from '../test/fixtures';
import { ListeAlertes } from './ListeAlertes';

let fetchMock: ReturnType<typeof vi.fn>;

function afficher() {
  // Type infere depuis vi.fn() : l'annoter explicitement en ReturnType<typeof
  // vi.fn> elargirait la signature et ne satisferait plus la propriete.
  const onNonAutorise = vi.fn((_message: string | null) => {});
  render(<ListeAlertes jeton="jeton.de.test" onNonAutorise={onNonAutorise} />);
  return { onNonAutorise };
}

/** Les lignes de donnees, en-tete de tableau exclu. */
function lignesAlertes() {
  const tableau = screen.getByRole('table');
  const corps = within(tableau).getAllByRole('rowgroup')[1];
  return within(corps).getAllByRole('row');
}

beforeEach(() => {
  fetchMock = vi.fn().mockResolvedValue(reponseJson(ALERTES_API));
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('ListeAlertes - chargement', () => {
  it('appelle GET /api/alertes au montage avec le jeton', async () => {
    afficher();

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

    expect(String(fetchMock.mock.calls[0][0])).toContain('/api/alertes');

    const options = fetchMock.mock.calls[0][1] as RequestInit;
    const entetes = options.headers as Record<string, string>;
    expect(entetes.Authorization).toBe('Bearer jeton.de.test');
  });

  it('affiche un etat de chargement avant la reponse', async () => {
    // Promesse volontairement non resolue : le composant reste en chargement.
    let resoudre: (reponse: Response) => void = () => {};
    fetchMock.mockReturnValue(new Promise<Response>((res) => { resoudre = res; }));

    afficher();

    expect(screen.getByRole('status')).toHaveTextContent(/Chargement des alertes/i);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();

    // On resout pour ne pas laisser de promesse pendante apres le test.
    await act(async () => {
      resoudre(reponseJson(ALERTES_API));
    });
  });
});

describe('ListeAlertes - affichage des donnees', () => {
  it('affiche une ligne par alerte, dans l ordre fourni par l API', async () => {
    afficher();

    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument());

    const lignes = lignesAlertes();
    expect(lignes).toHaveLength(ALERTES_API.length);

    // L'API trie par date decroissante : on n'inverse rien cote client.
    expect(within(lignes[0]).getByText('VH-001')).toBeInTheDocument();
    expect(within(lignes[1]).getByText('VH-002')).toBeInTheDocument();
    expect(within(lignes[2]).getByText('VH-003')).toBeInTheDocument();
  });

  it('traduit les types d alerte en libelles francais', async () => {
    afficher();

    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument());

    expect(screen.getAllByText('Vitesse excessive')).toHaveLength(2);
    expect(screen.getByText('Sortie de zone')).toBeInTheDocument();
  });

  it('affiche la severite de chaque alerte', async () => {
    afficher();

    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument());

    expect(screen.getByText('Critique')).toBeInTheDocument();
    expect(screen.getByText('Alerte')).toBeInTheDocument();
    expect(screen.getByText('Avertissement')).toBeInTheDocument();
  });

  it('affiche les details et une date lisible', async () => {
    afficher();

    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument());

    const premiere = lignesAlertes()[0];
    expect(within(premiere).getByText(/92,0 km\/h/)).toBeInTheDocument();

    // La date est formatee dans le fuseau local : on verifie qu'elle est
    // rendue et porte bien l'annee, sans dependre du decalage horaire.
    expect(within(premiere).getByText(/2026/)).toBeInTheDocument();
  });

  it('affiche le compteur d alertes', async () => {
    afficher();

    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument());

    expect(screen.getByText(String(ALERTES_API.length))).toBeInTheDocument();
  });
});

describe('ListeAlertes - cas limites', () => {
  it('affiche un message dedie quand aucune alerte n existe', async () => {
    fetchMock.mockResolvedValue(reponseJson([]));

    afficher();

    await waitFor(() =>
      expect(screen.getByText('Aucune alerte pour le moment')).toBeInTheDocument(),
    );

    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('signale au parent une reponse 401 sans afficher d erreur locale', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: 'non autorise' }, 401));

    const { onNonAutorise: rappel } = afficher();

    await waitFor(() => expect(rappel).toHaveBeenCalledTimes(1));

    // Le message vient d'api.ts, pas du composant.
    expect(rappel.mock.calls[0][0]).toMatch(/session a expire/i);

    // Aucune table ni message d'erreur : le parent bascule vers la connexion.
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('affiche l erreur et ne deconnecte pas sur un echec non lie au jeton', async () => {
    fetchMock.mockResolvedValue(reponseJson({}, 500));

    const { onNonAutorise: rappel } = afficher();

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());

    expect(screen.getByRole('alert')).toHaveTextContent(/500/);
    expect(rappel).not.toHaveBeenCalled();
  });

  it('affiche l erreur quand l API est injoignable', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    afficher();

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());

    expect(screen.getByRole('alert')).toHaveTextContent(/API injoignable/i);
  });
});
