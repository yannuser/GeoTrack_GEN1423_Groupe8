import { Button } from 'react-bootstrap';
import { initiales, type SessionUtilisateur } from '../auth';
import { ORDRE_STATUTS, STATUTS, type StatutVehicule } from '../types';

/** GEO-59 : les deux vues de l'application. */
export type Vue = 'carte' | 'alertes';

const ONGLETS: { vue: Vue; libelle: string }[] = [
  { vue: 'carte', libelle: 'Carte' },
  { vue: 'alertes', libelle: 'Alertes' },
];

interface Props {
  compteurs: Record<StatutVehicule, number>;
  session: SessionUtilisateur;
  onDeconnexion: () => void;
  vue: Vue;
  onVue: (vue: Vue) => void;
}

/**
 * Bandeau superieur : logo, navigation, compteurs par statut, utilisateur.
 * Depuis GEO-18, le bloc utilisateur reflete la session reelle et le bouton
 * "Deconnexion" purge le jeton.
 *
 * GEO-59 ajoute la bascule carte/alertes. Le projet n'utilise pas React Router :
 * la navigation reste un simple etat local remonte a App, sans dependance
 * supplementaire ni URL a gerer.
 */
export function EnTete({ compteurs, session, onDeconnexion, vue, onVue }: Props) {
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

      <nav className="gt-entete__navigation" aria-label="Vues">
        {ONGLETS.map((onglet) => (
          <button
            key={onglet.vue}
            type="button"
            className={`gt-onglet${vue === onglet.vue ? ' gt-onglet--actif' : ''}`}
            onClick={() => onVue(onglet.vue)}
            aria-current={vue === onglet.vue ? 'page' : undefined}
          >
            {onglet.libelle}
          </button>
        ))}
      </nav>

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
