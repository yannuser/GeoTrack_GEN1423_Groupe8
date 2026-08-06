import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { CLE_SESSION, installerSession, POSITIONS_API, reponseJson } from './test/fixtures';

vi.mock('react-leaflet', () => import('./test/leafletMock'));

/** Doit rester aligne sur INTERVALLE_RAFRAICHISSEMENT_MS dans App.tsx. */
const INTERVALLE_MS = 5_000;

let fetchMock: ReturnType<typeof vi.fn>;

/** Monte App puis laisse le chargement initial se resoudre (timers factices). */
async function monterApp() {
  render(<App />);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
}

async function avancer(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock = vi.fn().mockResolvedValue(reponseJson(POSITIONS_API));
  vi.stubGlobal('fetch', fetchMock);
  // GEO-18 : sans session, App n'affiche que l'ecran de connexion.
  installerSession();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('App - chargement des positions', () => {
  it('appelle l API au montage et affiche un marqueur par vehicule', async () => {
    await monterApp();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0][0])).toContain('/api/positionsgps');

    expect(screen.getAllByTestId('marqueur')).toHaveLength(POSITIONS_API.length);
  });

  it('joint le jeton de session dans l en-tete Authorization', async () => {
    await monterApp();

    const options = fetchMock.mock.calls[0][1];
    expect(options.headers.Authorization).toBe('Bearer jeton.de.test');
  });

  it('colore les marqueurs selon le statut renvoye par l API', async () => {
    await monterApp();

    const couleurs = screen
      .getAllByTestId('marqueur')
      .map((marqueur) => marqueur.dataset.couleur);

    // Ordre alphabetique par vehiculeId : VH-001 (en route), VH-002 (arret), VH-003 (panne).
    expect(couleurs).toEqual(['#22a05b', '#8a8f98', '#e03131']);
  });

  it('alimente le panneau lateral avec les identifiants et les vitesses', async () => {
    await monterApp();

    // On se limite au panneau : les identifiants apparaissent aussi dans les popups.
    const panneau = screen.getByRole('list');

    for (const position of POSITIONS_API) {
      expect(within(panneau).getByText(position.vehiculeId)).toBeInTheDocument();
    }
    expect(panneau).toHaveTextContent('62 km/h');
  });

  it('ne conserve que la position la plus recente de chaque vehicule', async () => {
    const positionAncienne = {
      ...POSITIONS_API[0],
      id: 99,
      vitesse: 5,
      horodatage: new Date(Date.parse(POSITIONS_API[0].horodatage) - 60_000).toISOString(),
    };
    fetchMock.mockResolvedValue(reponseJson([positionAncienne, ...POSITIONS_API]));

    await monterApp();

    expect(screen.getAllByTestId('marqueur')).toHaveLength(3);
    expect(screen.getByRole('list')).toHaveTextContent('62 km/h');
    expect(screen.getByRole('list')).not.toHaveTextContent('5 km/h');
  });
});

describe('App - rafraichissement automatique', () => {
  it('declenche un nouvel appel API toutes les 5 secondes', async () => {
    await monterApp();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    // Juste avant l'echeance : toujours un seul appel.
    await avancer(INTERVALLE_MS - 1);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await avancer(1);
    expect(fetchMock).toHaveBeenCalledTimes(2);

    await avancer(INTERVALLE_MS);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('affiche les donnees renvoyees par le rafraichissement', async () => {
    fetchMock.mockResolvedValueOnce(reponseJson([POSITIONS_API[0]]));
    await monterApp();
    expect(screen.getAllByTestId('marqueur')).toHaveLength(1);

    fetchMock.mockResolvedValue(reponseJson(POSITIONS_API));
    await avancer(INTERVALLE_MS);

    expect(screen.getAllByTestId('marqueur')).toHaveLength(3);
  });

  it('arrete le minuteur au demontage', async () => {
    const { unmount } = render(<App />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);

    unmount();
    await avancer(INTERVALLE_MS * 3);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe('App - gestion des erreurs', () => {
  it('affiche un message d erreur sans planter quand fetch echoue', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    await monterApp();

    const alerte = screen.getByRole('alert');
    expect(alerte).toBeInTheDocument();
    expect(alerte).toHaveTextContent(/API injoignable/i);

    // L'application reste montee et affiche un etat vide coherent.
    expect(screen.getByTestId('carte')).toBeInTheDocument();
    expect(screen.queryAllByTestId('marqueur')).toHaveLength(0);
    expect(screen.getByText(/Aucun vehicule ne correspond aux filtres/i)).toBeInTheDocument();
  });

  it('affiche le code HTTP quand l API repond en erreur', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: 'boom' }, 500));

    await monterApp();

    expect(screen.getByRole('alert')).toHaveTextContent('500');
    expect(screen.queryAllByTestId('marqueur')).toHaveLength(0);
  });

  it('continue a interroger l API apres une erreur et efface l alerte au retour', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'));

    await monterApp();
    expect(screen.getByRole('alert')).toBeInTheDocument();

    fetchMock.mockResolvedValue(reponseJson(POSITIONS_API));
    await avancer(INTERVALLE_MS);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getAllByTestId('marqueur')).toHaveLength(3);
  });
});

describe('App - porte d authentification (GEO-18)', () => {
  const champIdentifiant = () => screen.queryByLabelText('Identifiant');

  it('affiche l ecran de connexion et n appelle pas l API sans session', async () => {
    localStorage.clear();

    render(<App />);
    await avancer(0);

    expect(champIdentifiant()).toBeInTheDocument();
    expect(screen.queryByTestId('carte')).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('affiche le suivi de flotte quand une session valide est presente', async () => {
    await monterApp();

    expect(champIdentifiant()).not.toBeInTheDocument();
    expect(screen.getByTestId('carte')).toBeInTheDocument();
    expect(screen.getByText('Jean Dubois')).toBeInTheDocument();
  });

  it('renvoie vers la connexion quand l API repond 401', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: 'non autorise' }, 401));

    await monterApp();

    expect(champIdentifiant()).toBeInTheDocument();
    expect(screen.queryByTestId('carte')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(/session a expire/i);

    // La session invalide est purgee, et plus rien n'est reinterroge.
    expect(localStorage.getItem(CLE_SESSION)).toBeNull();
    const appelsAvant = fetchMock.mock.calls.length;
    await avancer(INTERVALLE_MS * 3);
    expect(fetchMock).toHaveBeenCalledTimes(appelsAvant);
  });

  it('la deconnexion purge la session et coupe le rafraichissement', async () => {
    await monterApp();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: /deconnexion/i }));

    expect(champIdentifiant()).toBeInTheDocument();
    expect(localStorage.getItem(CLE_SESSION)).toBeNull();

    await avancer(INTERVALLE_MS * 3);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('bascule sur le suivi de flotte apres une connexion reussie', async () => {
    localStorage.clear();

    const nouvelleSession = {
      jeton: 'jeton.frais',
      expiration: new Date(Date.now() + 3_600_000).toISOString(),
      identifiant: 'marie.tremblay',
      nomComplet: 'Marie Tremblay',
    };

    // Chaque endpoint repond selon son URL : login puis positions.
    fetchMock.mockImplementation((url: unknown) =>
      Promise.resolve(
        String(url).includes('/api/auth/login')
          ? reponseJson(nouvelleSession)
          : reponseJson(POSITIONS_API),
      ),
    );

    render(<App />);
    await avancer(0);

    fireEvent.change(screen.getByLabelText('Identifiant'), {
      target: { value: 'marie.tremblay' },
    });
    fireEvent.change(screen.getByLabelText('Mot de passe'), {
      target: { value: 'MotDePasse1' },
    });

    fireEvent.click(screen.getByRole('button', { name: /se connecter/i }));
    await avancer(0);

    // Connecte : la carte est montee et l'en-tete affiche le nom du compte.
    expect(screen.getByTestId('carte')).toBeInTheDocument();
    expect(screen.getByText('Marie Tremblay')).toBeInTheDocument();

    // Le chargement des positions utilise le jeton tout juste obtenu.
    const appelPositions = fetchMock.mock.calls.find(([url]) =>
      String(url).includes('/api/positionsgps'),
    );
    expect(appelPositions).toBeDefined();
    expect(appelPositions![1].headers.Authorization).toBe('Bearer jeton.frais');
  });
});
