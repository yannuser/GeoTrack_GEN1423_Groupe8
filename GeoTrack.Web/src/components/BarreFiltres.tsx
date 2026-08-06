import { Form, InputGroup } from 'react-bootstrap';
import { ORDRE_STATUTS, STATUTS, type StatutVehicule } from '../types';

interface Props {
  recherche: string;
  onRecherche: (valeur: string) => void;
  zones: string[];
  zoneSelectionnee: string;
  onZone: (zone: string) => void;
  statutsActifs: Set<StatutVehicule>;
  onBasculerStatut: (statut: StatutVehicule) => void;
  compteurs: Record<StatutVehicule, number>;
  secondesDepuisMaj: number | null;
  enChargement: boolean;
}

export const TOUTES_ZONES = 'toutes';

/** Barre superieure de la zone carte : recherche, zone, pastilles de statut, fraicheur. */
export function BarreFiltres({
  recherche,
  onRecherche,
  zones,
  zoneSelectionnee,
  onZone,
  statutsActifs,
  onBasculerStatut,
  compteurs,
  secondesDepuisMaj,
  enChargement,
}: Props) {
  return (
    <div className="gt-barre">
      <InputGroup className="gt-recherche">
        <InputGroup.Text>
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" strokeLinecap="round" />
          </svg>
        </InputGroup.Text>
        <Form.Control
          type="search"
          placeholder="Rechercher un vehicule..."
          value={recherche}
          onChange={(evenement) => onRecherche(evenement.target.value)}
          aria-label="Rechercher un vehicule"
        />
      </InputGroup>

      <Form.Select
        className="gt-zones"
        value={zoneSelectionnee}
        onChange={(evenement) => onZone(evenement.target.value)}
        aria-label="Filtrer par zone"
      >
        <option value={TOUTES_ZONES}>Toutes les zones</option>
        {zones.map((zone) => (
          <option value={zone} key={zone}>
            {zone}
          </option>
        ))}
      </Form.Select>

      <div className="gt-pastilles" role="group" aria-label="Filtrer par statut">
        {ORDRE_STATUTS.map((statut) => {
          const actif = statutsActifs.has(statut);
          return (
            <button
              type="button"
              key={statut}
              className={`gt-pastille gt-pastille--${statut}${actif ? ' gt-pastille--active' : ''}`}
              onClick={() => onBasculerStatut(statut)}
              aria-pressed={actif}
            >
              <span className="gt-point" style={{ background: STATUTS[statut].couleur }} />
              {compteurs[statut]} {STATUTS[statut].libelle}
            </button>
          );
        })}
      </div>

      <span className="gt-fraicheur">
        <span className={`gt-point${enChargement ? ' gt-point--pulse' : ''}`} style={{ background: '#22a05b' }} />
        {secondesDepuisMaj === null ? (
          'Connexion...'
        ) : (
          <>
            Mise a jour il y a <strong>{secondesDepuisMaj}s</strong>
          </>
        )}
      </span>
    </div>
  );
}
