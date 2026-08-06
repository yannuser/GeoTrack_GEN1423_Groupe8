import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CLE_SESSION, reponseJson, sessionValide } from '../test/fixtures';
import { FormulaireConnexion } from './FormulaireConnexion';

const MESSAGE_GENERIQUE = 'Identifiant ou mot de passe incorrect';

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function afficher(messageInitial: string | null = null) {
  const onConnecte = vi.fn();
  render(<FormulaireConnexion onConnecte={onConnecte} messageInitial={messageInitial} />);
  return { onConnecte, utilisateur: userEvent.setup() };
}

const champIdentifiant = () => screen.getByLabelText('Identifiant');
const champMotDePasse = () => screen.getByLabelText('Mot de passe');
const boutonConnexion = () => screen.getByRole('button', { name: /se connecter/i });

describe('FormulaireConnexion', () => {
  it('affiche les deux champs et le bouton', () => {
    afficher();

    expect(champIdentifiant()).toBeInTheDocument();
    expect(champMotDePasse()).toBeInTheDocument();
    expect(boutonConnexion()).toBeInTheDocument();
    expect(champMotDePasse()).toHaveAttribute('type', 'password');
  });

  it('desactive le bouton tant que les deux champs ne sont pas remplis', async () => {
    const { utilisateur } = afficher();

    expect(boutonConnexion()).toBeDisabled();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    expect(boutonConnexion()).toBeDisabled();

    await utilisateur.type(champMotDePasse(), 'MotDePasse1');
    expect(boutonConnexion()).toBeEnabled();
  });

  it('appelle POST /api/auth/login avec les identifiants saisis', async () => {
    fetchMock.mockResolvedValue(reponseJson(sessionValide()));
    const { utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MotDePasse1');
    await utilisateur.click(boutonConnexion());

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

    const [url, options] = fetchMock.mock.calls[0];
    expect(String(url)).toContain('/api/auth/login');
    expect(options.method).toBe('POST');
    expect(JSON.parse(options.body)).toEqual({
      identifiant: 'jean.dubois',
      motDePasse: 'MotDePasse1',
    });
  });

  it('remonte la session et la persiste apres une connexion reussie', async () => {
    const session = sessionValide();
    fetchMock.mockResolvedValue(reponseJson(session));
    const { onConnecte, utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MotDePasse1');
    await utilisateur.click(boutonConnexion());

    await waitFor(() => expect(onConnecte).toHaveBeenCalledTimes(1));
    expect(onConnecte).toHaveBeenCalledWith(session);

    expect(JSON.parse(localStorage.getItem(CLE_SESSION)!)).toEqual(session);
  });

  it('affiche le message generique renvoye par l API en cas d echec', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: MESSAGE_GENERIQUE }, 401));
    const { onConnecte, utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MauvaisMdp9');
    await utilisateur.click(boutonConnexion());

    const alerte = await screen.findByRole('alert');
    expect(alerte).toHaveTextContent(MESSAGE_GENERIQUE);

    // Aucune session ouverte, rien de persiste.
    expect(onConnecte).not.toHaveBeenCalled();
    expect(localStorage.getItem(CLE_SESSION)).toBeNull();
  });

  it('ne revele pas lequel des deux champs est errone', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: MESSAGE_GENERIQUE }, 401));
    const { utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'inconnu');
    await utilisateur.type(champMotDePasse(), 'MauvaisMdp9');
    await utilisateur.click(boutonConnexion());

    const alerte = await screen.findByRole('alert');
    const texte = alerte.textContent ?? '';

    // Le message mentionne les deux champs sur un pied d'egalite ("identifiant
    // OU mot de passe") et ne designe jamais celui qui est en cause.
    expect(texte).toBe(MESSAGE_GENERIQUE);
    expect(texte).not.toMatch(/inconnu|introuvable|inexistant/i);
    expect(texte).not.toMatch(/verrouill|bloqu/i);
    expect(texte).not.toMatch(/^mot de passe/i);
    expect(texte).not.toMatch(/^identifiant (?!ou )/i);
  });

  it('vide le mot de passe apres un echec, sans toucher a l identifiant', async () => {
    fetchMock.mockResolvedValue(reponseJson({ message: MESSAGE_GENERIQUE }, 401));
    const { utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MauvaisMdp9');
    await utilisateur.click(boutonConnexion());

    await screen.findByRole('alert');

    expect(champMotDePasse()).toHaveValue('');
    expect(champIdentifiant()).toHaveValue('jean.dubois');
  });

  it('ne plante pas si l API est injoignable', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
    const { onConnecte, utilisateur } = afficher();

    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MotDePasse1');
    await utilisateur.click(boutonConnexion());

    const alerte = await screen.findByRole('alert');
    expect(alerte).toHaveTextContent(/API injoignable/i);

    expect(onConnecte).not.toHaveBeenCalled();
    expect(boutonConnexion()).toBeInTheDocument();
  });

  it('affiche le message initial transmis par App (session expiree)', () => {
    afficher('Votre session a expire. Veuillez vous reconnecter.');

    expect(screen.getByRole('alert')).toHaveTextContent(/session a expire/i);
  });

  it('bloque les soumissions concurrentes pendant l appel', async () => {
    let resoudre: ((valeur: Response) => void) | undefined;
    fetchMock.mockReturnValue(
      new Promise<Response>((resolution) => {
        resoudre = resolution;
      }),
    );

    const { utilisateur } = afficher();
    await utilisateur.type(champIdentifiant(), 'jean.dubois');
    await utilisateur.type(champMotDePasse(), 'MotDePasse1');
    await utilisateur.click(boutonConnexion());

    // Requete en vol : le bouton passe en attente et n'accepte plus de clic.
    const enAttente = await screen.findByRole('button', { name: /connexion\.\.\./i });
    expect(enAttente).toBeDisabled();

    await act(async () => {
      resoudre!(reponseJson(sessionValide()));
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
