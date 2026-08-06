import { Button } from 'react-bootstrap';
import { initiales, type SessionUtilisateur } from '../auth';
import { ORDRE_STATUTS, STATUTS, type StatutVehicule } from '../types';

interface Props {
  compteurs: Record<StatutVehicule, number>;
  session: SessionUtilisateur;
  onDeconnexion: () => void;
}

/**
 * Bandeau superieur : logo, compteurs par statut, utilisateur connecte.
 * Depuis GEO-18, le bloc utilisateur reflete la session reelle et le bouton
 * "Deconnexion" purge le jeton.
 */
export function EnTete({ compteurs, session, onDeconnexion }: Props) {
  return (
    <header className="gt-entete">
      <div className="gt-entete__marque">
        <span className="gt-logo" aria-hidden="true">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5A2.5 2.5 0 1 1 12 6.5a2.5 2.5 0 0 1 0 5z" />
          </svg>
        </span>
        <span className="gt-entete__titre">GeoTrack</span>
        <span className="gt-entete__separateur" />
        <span className="gt-entete__sous-titre">GESTION DE FLOTTE</span>
      </div>

      <div className="gt-entete__compteurs">
        {ORDRE_STATUTS.map((statut) => (
          <span className="gt-compteur" key={statut}>
            <span className="gt-point" style={{ background: STATUTS[statut].couleur }} />
            <strong>{compteurs[statut]}</strong> {STATUTS[statut].libelle}
          </span>
        ))}
      </div>

      <div className="gt-entete__utilisateur">
        <span className="gt-avatar">{initiales(session.nomComplet, session.identifiant)}</span>
        <span className="gt-entete__identite">
          <strong>{session.nomComplet || session.identifiant}</strong>
          <small>Gestionnaire de flotte</small>
        </span>
        <Button
          variant="outline-secondary"
          size="sm"
          className="gt-deconnexion"
          onClick={onDeconnexion}
        >
          Deconnexion
        </Button>
      </div>
    </header>
  );
}
