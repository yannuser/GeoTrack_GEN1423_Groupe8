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

// ---------------------------------------------------------------------------
// GEO-59 : alertes centralisees (GET /api/alertes)
// ---------------------------------------------------------------------------

/**
 * Contrat de GeoTrack.Api/Models/Alerte.cs.
 *
 * ATTENTION : `typeAlerte` et `severite` arrivent en ENTIERS, pas en chaines.
 * L'API n'enregistre aucun JsonStringEnumConverter, donc System.Text.Json
 * serialise les enums par leur valeur numerique. Les tables de correspondance
 * ci-dessous sont donc indexees par nombre.
 */
export interface Alerte {
  id: number;
  date: string;
  vehiculeId: string;
  typeAlerte: number;
  severite: number;
  details: string;
}

/** GeoTrack.Api/Models/TypeAlerte.cs */
export const TYPE_ALERTE_VITESSE = 0;
export const TYPE_ALERTE_SORTIE_ZONE = 1;

/** GeoTrack.Api/Services/GEO-51 : SeveriteAlerte */
export const SEVERITE_AUCUNE = 0;
export const SEVERITE_AVERTISSEMENT = 1;
export const SEVERITE_ALERTE = 2;
export const SEVERITE_CRITIQUE = 3;

const LIBELLES_TYPE_ALERTE: Record<number, string> = {
  [TYPE_ALERTE_VITESSE]: 'Vitesse excessive',
  [TYPE_ALERTE_SORTIE_ZONE]: 'Sortie de zone',
};

export interface SeveriteInfo {
  libelle: string;
  couleur: string;
  variantBadge: string;
}

/**
 * Couleurs alignees sur celles des statuts vehicule (voir STATUTS) : meme rouge
 * pour le niveau le plus grave, meme gris pour l'absence de signal. Les deux
 * niveaux intermediaires reprennent la graduation orange puis jaune.
 */
const SEVERITES: Record<number, SeveriteInfo> = {
  [SEVERITE_AUCUNE]: { libelle: 'Aucune', couleur: '#8a8f98', variantBadge: 'secondary' },
  [SEVERITE_AVERTISSEMENT]: { libelle: 'Avertissement', couleur: '#f59f00', variantBadge: 'warning' },
  [SEVERITE_ALERTE]: { libelle: 'Alerte', couleur: '#f76707', variantBadge: 'warning' },
  [SEVERITE_CRITIQUE]: { libelle: 'Critique', couleur: '#e03131', variantBadge: 'danger' },
};

/** Repli explicite : une valeur inconnue s'affiche au lieu de casser le rendu. */
export function libelleTypeAlerte(type: number): string {
  return LIBELLES_TYPE_ALERTE[type] ?? `Type ${type}`;
}

export function infoSeverite(severite: number): SeveriteInfo {
  return (
    SEVERITES[severite] ?? {
      libelle: `Severite ${severite}`,
      couleur: '#8a8f98',
      variantBadge: 'secondary',
    }
  );
}

/** Date lisible dans le fuseau du navigateur : "8 aout 2026, 14:32". */
export function formaterDateAlerte(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;

  return date.toLocaleString('fr-CA', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
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
