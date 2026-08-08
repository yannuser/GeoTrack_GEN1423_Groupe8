import type { SessionUtilisateur } from '../auth';
import type { Alerte, PositionGps, Vehicule } from '../types';
import { versVehicules } from '../types';

/** Horodatage fixe : evite tout test dependant de l'heure reelle. */
export const MAINTENANT = new Date('2026-08-06T12:00:00.000Z');

function ilYaSecondes(secondes: number): string {
  return new Date(MAINTENANT.getTime() - secondes * 1000).toISOString();
}

/**
 * Trois positions couvrant les trois statuts de la maquette GEO-24 :
 * en route (vert), a l'arret (gris), panne (rouge).
 */
export const POSITIONS_API: PositionGps[] = [
  {
    id: 1,
    vehiculeId: 'VH-001',
    latitude: 45.4765,
    longitude: -75.7013,
    vitesse: 62.4,
    direction: 90,
    horodatage: ilYaSecondes(10),
    etatVehicule: 'En route',
    niveauCarburant: 72,
    erreur: null,
  },
  {
    id: 2,
    vehiculeId: 'VH-002',
    latitude: 45.4201,
    longitude: -75.699,
    vitesse: 0,
    direction: 0,
    horodatage: ilYaSecondes(45),
    etatVehicule: "A l'arret",
    niveauCarburant: 40,
    erreur: null,
  },
  {
    id: 3,
    vehiculeId: 'VH-003',
    latitude: 45.5017,
    longitude: -75.6503,
    vitesse: 0,
    direction: 180,
    horodatage: ilYaSecondes(120),
    etatVehicule: 'Panne',
    niveauCarburant: 5,
    erreur: 'Capteur GPS hors service',
  },
];

/** Les memes vehicules, apres la normalisation appliquee par `versVehicules`. */
export const VEHICULES: Vehicule[] = versVehicules(POSITIONS_API);

/** Reponse `fetch` minimale mais suffisante pour `obtenirPositions`. */
export function reponseJson(donnees: unknown, statut = 200): Response {
  return {
    ok: statut >= 200 && statut < 300,
    status: statut,
    statusText: statut === 200 ? 'OK' : 'Erreur',
    json: async () => donnees,
  } as Response;
}

// ---------------------------------------------------------------------------
// GEO-18 : session authentifiee
// ---------------------------------------------------------------------------

export const CLE_SESSION = 'geotrack.session';

/** Session valide, expirant largement apres la fin du test. */
export function sessionValide(): SessionUtilisateur {
  return {
    jeton: 'jeton.de.test',
    expiration: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    identifiant: 'jean.dubois',
    nomComplet: 'Jean Dubois',
  };
}

/** Place une session valide dans localStorage : App demarre alors connecte. */
export function installerSession(session: SessionUtilisateur = sessionValide()): SessionUtilisateur {
  localStorage.setItem(CLE_SESSION, JSON.stringify(session));
  return session;
}

// ---------------------------------------------------------------------------
// GEO-59 : alertes centralisees
// ---------------------------------------------------------------------------

/**
 * Trois alertes couvrant les deux types et trois severites.
 * Deja triees par date decroissante, comme les renvoie l'API (GEO-58).
 *
 * Rappel : typeAlerte et severite sont des ENTIERS, l'API ne serialisant pas
 * les enums en chaines.
 */
export const ALERTES_API: Alerte[] = [
  {
    id: 3,
    date: '2026-08-08T14:32:00.000Z',
    vehiculeId: 'VH-001',
    typeAlerte: 0, // VitesseExcessive
    severite: 3, // Critique
    details: 'Vitesse relevee 92,0 km/h pour un seuil de 75,0 km/h (etat Declenchee).',
  },
  {
    id: 2,
    date: '2026-08-08T11:05:00.000Z',
    vehiculeId: 'VH-002',
    typeAlerte: 1, // SortieZone
    severite: 2, // Alerte
    details: "Sortie de la zone 'Depot central' (#1) — 1 240 m du centre pour un rayon de 500 m.",
  },
  {
    id: 1,
    date: '2026-08-07T09:15:00.000Z',
    vehiculeId: 'VH-003',
    typeAlerte: 0, // VitesseExcessive
    severite: 1, // Avertissement
    details: 'Vitesse relevee 58,0 km/h pour un seuil de 55,0 km/h (etat Declenchee).',
  },
];
