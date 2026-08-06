import type { SessionUtilisateur } from './auth';
import type { PositionGps } from './types';

/**
 * URL de base de GeoTrack.Api.
 * Surchargeable via VITE_API_URL (voir .env.development).
 * Par defaut : profil "http" de GeoTrack.Api/Properties/launchSettings.json.
 */
const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5288';

export class ApiError extends Error {
  readonly statut?: number;

  constructor(message: string, statut?: number) {
    super(message);
    this.name = 'ApiError';
    this.statut = statut;
  }
}

/** Vrai si l'echec vient d'un jeton absent, invalide ou expire (GEO-18). */
export function estNonAutorise(cause: unknown): boolean {
  return cause instanceof ApiError && cause.statut === 401;
}

const ERREUR_INJOIGNABLE =
  `API injoignable sur ${BASE_URL}. Verifiez que GeoTrack.Api est demarre (dotnet run).`;

/**
 * POST /api/auth/login (GEO-18).
 * L'API ne distingue jamais "identifiant inconnu" de "mot de passe errone" :
 * on remonte tel quel son message generique.
 */
export async function connexion(
  identifiant: string,
  motDePasse: string,
  signal?: AbortSignal,
): Promise<SessionUtilisateur> {
  let reponse: Response;

  try {
    reponse = await fetch(`${BASE_URL}/api/auth/login`, {
      method: 'POST',
      signal,
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ identifiant, motDePasse }),
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause;
    throw new ApiError(ERREUR_INJOIGNABLE);
  }

  if (!reponse.ok) {
    const message = await lireMessageErreur(reponse);
    throw new ApiError(message, reponse.status);
  }

  return (await reponse.json()) as SessionUtilisateur;
}

/** Recupere le message renvoye par l'API, avec un repli si le corps est illisible. */
async function lireMessageErreur(reponse: Response): Promise<string> {
  try {
    const corps = (await reponse.json()) as { message?: string };
    if (corps?.message) return corps.message;
  } catch {
    // Corps vide ou non-JSON : on retombe sur le message par defaut.
  }

  return reponse.status === 401
    ? 'Identifiant ou mot de passe incorrect'
    : `L'API a repondu ${reponse.status} ${reponse.statusText}.`;
}

/**
 * GET /api/positionsgps — les 50 dernieres positions, triees par
 * horodatage decroissant (GEO-8, PositionsGpsController.Dernieres).
 * Protege par [Authorize] depuis GEO-18 : le jeton est obligatoire.
 */
export async function obtenirPositions(
  jeton: string,
  signal?: AbortSignal,
): Promise<PositionGps[]> {
  let reponse: Response;

  try {
    reponse = await fetch(`${BASE_URL}/api/positionsgps`, {
      signal,
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${jeton}`,
      },
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause;
    throw new ApiError(ERREUR_INJOIGNABLE);
  }

  if (!reponse.ok) {
    if (reponse.status === 401) {
      throw new ApiError('Votre session a expire. Veuillez vous reconnecter.', 401);
    }
    throw new ApiError(`L'API a repondu ${reponse.status} ${reponse.statusText}.`, reponse.status);
  }

  return (await reponse.json()) as PositionGps[];
}
