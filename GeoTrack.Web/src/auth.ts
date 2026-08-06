/**
 * GEO-18 : session utilisateur cote navigateur.
 *
 * Le jeton JWT est conserve dans localStorage pour survivre a un rechargement.
 * LIMITE CONNUE : localStorage est lisible par tout script de la page, donc
 * vulnerable au XSS. Un cookie HttpOnly + SameSite serait plus sur, mais impose
 * que l'API et le front partagent un domaine — a revoir au deploiement.
 */

/** Reponse de POST /api/auth/login (GeoTrack.Api/Models/Auth/ReponseConnexion.cs). */
export interface SessionUtilisateur {
  jeton: string;
  /** Expiration du jeton en ISO 8601 (UTC). */
  expiration: string;
  identifiant: string;
  nomComplet: string;
}

const CLE_STOCKAGE = 'geotrack.session';

function estExpiree(session: SessionUtilisateur): boolean {
  const echeance = Date.parse(session.expiration);
  return Number.isNaN(echeance) || echeance <= Date.now();
}

/**
 * Session courante, ou null si absente, illisible ou expiree.
 * Une session expiree est purgee au passage : inutile de la retenter.
 */
export function lireSession(): SessionUtilisateur | null {
  let brut: string | null = null;

  try {
    brut = localStorage.getItem(CLE_STOCKAGE);
  } catch {
    // localStorage indisponible (mode prive, quota) : on repart sans session.
    return null;
  }

  if (!brut) return null;

  try {
    const session = JSON.parse(brut) as SessionUtilisateur;
    if (!session?.jeton || estExpiree(session)) {
      effacerSession();
      return null;
    }
    return session;
  } catch {
    effacerSession();
    return null;
  }
}

export function enregistrerSession(session: SessionUtilisateur): void {
  try {
    localStorage.setItem(CLE_STOCKAGE, JSON.stringify(session));
  } catch {
    // Echec d'ecriture : la session reste valable en memoire pour cet onglet.
  }
}

export function effacerSession(): void {
  try {
    localStorage.removeItem(CLE_STOCKAGE);
  } catch {
    // Rien a faire : il n'y avait de toute facon rien a purger.
  }
}

/**
 * "Jean Dubois" -> "JD" ; repli sur l'identifiant si le nom est absent.
 *
 * On decoupe d'abord sur les espaces pour que "Marie-Claire Tremblay" donne
 * "MT" et non "MC" : le prenom compose reste un seul mot. Ce n'est qu'a defaut
 * d'espace qu'on retombe sur les separateurs d'identifiant ("jean.dubois").
 */
export function initiales(nomComplet: string, identifiant: string): string {
  const source = nomComplet.trim() || identifiant.trim();
  if (!source) return '?';

  let mots = source.split(/\s+/).filter(Boolean);
  if (mots.length === 1) {
    mots = source.split(/[._-]+/).filter(Boolean);
  }

  const lettres = mots.slice(0, 2).map((mot) => mot[0]);

  return lettres.join('').toUpperCase() || '?';
}
