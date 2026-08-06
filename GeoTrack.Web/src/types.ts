/**
 * Contrat de l'API GeoTrack.Api (GEO-8).
 * Correspond exactement a GeoTrack.Api/Models/PositionGps.cs.
 * La serialisation ASP.NET Core utilise le camelCase par defaut.
 */
export interface PositionGps {
  id: number;
  vehiculeId: string;
  latitude: number;
  longitude: number;
  vitesse: number;
  direction: number;
  horodatage: string;
  etatVehicule: string;
  niveauCarburant: number | null;
  erreur: string | null;
}

/** Les trois statuts affiches par la maquette GEO-24. */
export type StatutVehicule = 'en_route' | 'a_l_arret' | 'panne';

export interface StatutInfo {
  libelle: string;
  couleur: string;
  variantBadge: string;
}

export const STATUTS: Record<StatutVehicule, StatutInfo> = {
  en_route: { libelle: 'En route', couleur: '#22a05b', variantBadge: 'success' },
  a_l_arret: { libelle: "A l'arret", couleur: '#8a8f98', variantBadge: 'secondary' },
  panne: { libelle: 'Panne', couleur: '#e03131', variantBadge: 'danger' },
};

/** Ordre d'affichage des statuts (en-tete, filtres, legende). */
export const ORDRE_STATUTS: StatutVehicule[] = ['en_route', 'a_l_arret', 'panne'];

/**
 * Vehicule tel qu'affiche par l'ecran : une position brute enrichie de
 * champs derives (statut normalise, libelle, zone, anciennete).
 */
export interface Vehicule {
  vehiculeId: string;
  nomAffiche: string;
  statut: StatutVehicule;
  vitesse: number;
  latitude: number;
  longitude: number;
  horodatage: Date;
  zone: string;
  erreur: string | null;
}

/**
 * Normalise le champ libre `etatVehicule` vers l'un des trois statuts.
 * L'API ne contraint pas ces valeurs (string sans enum ni validation),
 * d'ou cette tolerance sur les variantes rencontrees.
 */
export function normaliserStatut(position: PositionGps): StatutVehicule {
  if (position.erreur) return 'panne';

  const brut = (position.etatVehicule ?? '')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');

  if (brut.includes('panne') || brut.includes('erreur')) return 'panne';
  if (brut.includes('route') || brut.includes('mouvement')) return 'en_route';
  if (brut.includes('arret') || brut.includes('stop')) return 'a_l_arret';

  // Valeur inconnue : on se rabat sur la vitesse plutot que d'inventer un statut.
  return position.vitesse > 0 ? 'en_route' : 'a_l_arret';
}

/**
 * Zone geographique derivee des coordonnees.
 *
 * LIMITE CONNUE : le modele PositionGps ne porte aucune notion de zone.
 * Ce decoupage par bandes de longitude est un substitut provisoire, destine
 * a alimenter le filtre "Toutes les zones" de la maquette. A remplacer par
 * le vrai champ des que l'API l'exposera (voir GEO-9 / geofencing).
 */
export function deriverZone(longitude: number): string {
  const bande = Math.floor(((longitude + 180) % 180) / 0.05) % 3;
  return `Zone ${['A', 'B', 'C'][bande]}`;
}

/** "VH-001" ou "VEH-001" -> "Vehicule 001". */
export function deriverNomAffiche(vehiculeId: string): string {
  const chiffres = vehiculeId.match(/(\d+)\s*$/);
  return chiffres ? `Vehicule ${chiffres[1]}` : vehiculeId;
}

/**
 * L'API renvoie les 50 dernieres positions, tous vehicules confondus :
 * un meme vehicule peut donc apparaitre plusieurs fois. On ne conserve
 * que la position la plus recente de chacun.
 */
export function versVehicules(positions: PositionGps[]): Vehicule[] {
  const parVehicule = new Map<string, PositionGps>();

  for (const position of positions) {
    const existante = parVehicule.get(position.vehiculeId);
    if (!existante || new Date(position.horodatage) > new Date(existante.horodatage)) {
      parVehicule.set(position.vehiculeId, position);
    }
  }

  return [...parVehicule.values()]
    .map((position) => ({
      vehiculeId: position.vehiculeId,
      nomAffiche: deriverNomAffiche(position.vehiculeId),
      statut: normaliserStatut(position),
      vitesse: position.vitesse,
      latitude: position.latitude,
      longitude: position.longitude,
      horodatage: new Date(position.horodatage),
      zone: deriverZone(position.longitude),
      erreur: position.erreur,
    }))
    .sort((a, b) => a.vehiculeId.localeCompare(b.vehiculeId));
}

/** "il y a 14 min", "il y a 29s", "il y a 2 h". */
export function ancienneteCourte(date: Date, maintenant: Date = new Date()): string {
  const secondes = Math.max(0, Math.floor((maintenant.getTime() - date.getTime()) / 1000));
  if (secondes < 60) return `${secondes}s`;
  const minutes = Math.floor(secondes / 60);
  if (minutes < 60) return `${minutes} min`;
  const heures = Math.floor(minutes / 60);
  if (heures < 24) return `${heures} h`;
  return `${Math.floor(heures / 24)} j`;
}
