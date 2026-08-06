import { Badge } from 'react-bootstrap';
import {
  ancienneteCourte,
  ORDRE_STATUTS,
  STATUTS,
  type Vehicule,
} from '../types';

interface Props {
  vehicules: Vehicule[];
  vehiculeSelectionne: string | null;
  onSelection: (vehiculeId: string) => void;
  maintenant: Date;
}

/** Panneau lateral gauche : compteur, liste scrollable, legende. */
export function PanneauVehicules({
  vehicules,
  vehiculeSelectionne,
  onSelection,
  maintenant,
}: Props) {
  return (
    <aside className="gt-panneau">
      <div className="gt-panneau__entete">
        <h2 className="gt-panneau__titre">VEHICULES ACTIFS</h2>
        <p className="gt-panneau__compteur">
          <strong>{vehicules.length}</strong> <span>vehicules</span>
        </p>
      </div>

      <ul className="gt-liste">
        {vehicules.length === 0 && (
          <li className="gt-liste__vide">Aucun vehicule ne correspond aux filtres.</li>
        )}

        {vehicules.map((vehicule) => {
          const statut = STATUTS[vehicule.statut];
          const selectionne = vehicule.vehiculeId === vehiculeSelectionne;

          return (
            <li key={vehicule.vehiculeId}>
              <button
                type="button"
                className={`gt-carte-vehicule${selectionne ? ' gt-carte-vehicule--active' : ''}`}
                onClick={() => onSelection(vehicule.vehiculeId)}
                aria-pressed={selectionne}
              >
                <span className="gt-carte-vehicule__ligne1">
                  <span className="gt-carte-vehicule__id">{vehicule.vehiculeId}</span>
                  <Badge bg={statut.variantBadge} pill className="gt-badge">
                    {statut.libelle}
                  </Badge>
                </span>

                <span className="gt-carte-vehicule__ligne2">
                  <span className="gt-carte-vehicule__nom">{vehicule.nomAffiche}</span>
                  {vehicule.statut === 'en_route' ? (
                    <span className="gt-carte-vehicule__vitesse">
                      <strong>{Math.round(vehicule.vitesse)}</strong> km/h
                    </span>
                  ) : (
                    <span className="gt-carte-vehicule__vitesse gt-carte-vehicule__vitesse--nulle">
                      &mdash;
                    </span>
                  )}
                </span>

                <span className="gt-carte-vehicule__ligne3">
                  <span className="gt-point-mini" style={{ background: statut.couleur }} />
                  {ancienneteCourte(vehicule.horodatage, maintenant)} &middot; {vehicule.zone}
                </span>
              </button>
            </li>
          );
        })}
      </ul>

      <div className="gt-legende">
        <h3 className="gt-panneau__titre">LEGENDE</h3>
        {ORDRE_STATUTS.map((statut) => (
          <p className="gt-legende__ligne" key={statut}>
            <span className="gt-point" style={{ background: STATUTS[statut].couleur }} />
            {STATUTS[statut].libelle}
          </p>
        ))}
      </div>
    </aside>
  );
}
