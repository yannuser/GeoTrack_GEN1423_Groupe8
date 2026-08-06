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

/**
 * GET /api/positionsgps — les 50 dernieres positions, triees par
 * horodatage decroissant (GEO-8, PositionsGpsController.Dernieres).
 */
export async function obtenirPositions(signal?: AbortSignal): Promise<PositionGps[]> {
  let reponse: Response;

  try {
    reponse = await fetch(`${BASE_URL}/api/positionsgps`, {
      signal,
      headers: { Accept: 'application/json' },
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause;
    throw new ApiError(
      `API injoignable sur ${BASE_URL}. Verifiez que GeoTrack.Api est demarre (dotnet run).`,
    );
  }

  if (!reponse.ok) {
    throw new ApiError(`L'API a repondu ${reponse.status} ${reponse.statusText}.`, reponse.status);
  }

  return (await reponse.json()) as PositionGps[];
}
