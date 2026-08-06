import { useEffect, useRef } from 'react';
import { CircleMarker, MapContainer, Popup, TileLayer, Tooltip, useMap } from 'react-leaflet';
import { LatLngBounds } from 'leaflet';
import { ancienneteCourte, STATUTS, type Vehicule } from '../types';

interface Props {
  vehicules: Vehicule[];
  vehiculeSelectionne: string | null;
  onSelection: (vehiculeId: string) => void;
  maintenant: Date;
}

/** Centre par defaut : Gatineau / Ottawa, coherent avec les jeux de test du depot. */
const CENTRE_DEFAUT: [number, number] = [45.4765, -75.7013];
const ZOOM_DEFAUT = 12;

/**
 * Cadre la carte sur l'ensemble des vehicules au premier chargement,
 * puis recentre sur le vehicule selectionne sans re-zoomer ensuite.
 */
function ControleurVue({ vehicules, vehiculeSelectionne }: Omit<Props, 'onSelection' | 'maintenant'>) {
  const carte = useMap();
  const cadrageInitialFait = useRef(false);

  useEffect(() => {
    if (cadrageInitialFait.current || vehicules.length === 0) return;

    const bornes = new LatLngBounds(vehicules.map((v) => [v.latitude, v.longitude] as [number, number]));
    carte.fitBounds(bornes, { padding: [60, 60], maxZoom: 14 });
    cadrageInitialFait.current = true;
  }, [carte, vehicules]);

  useEffect(() => {
    if (!vehiculeSelectionne) return;

    const cible = vehicules.find((v) => v.vehiculeId === vehiculeSelectionne);
    if (cible) {
      carte.flyTo([cible.latitude, cible.longitude], Math.max(carte.getZoom(), 14), {
        duration: 0.6,
      });
    }
  }, [carte, vehiculeSelectionne, vehicules]);

  return null;
}

export function CarteVehicules({
  vehicules,
  vehiculeSelectionne,
  onSelection,
  maintenant,
}: Props) {
  return (
    <MapContainer
      center={CENTRE_DEFAUT}
      zoom={ZOOM_DEFAUT}
      className="gt-carte"
      scrollWheelZoom
      zoomControl={false}
    >
      {/* Fond OpenStreetMap : aucune cle API requise. */}
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        maxZoom={19}
      />

      <ControleurVue vehicules={vehicules} vehiculeSelectionne={vehiculeSelectionne} />

      {vehicules.map((vehicule) => {
        const statut = STATUTS[vehicule.statut];
        const selectionne = vehicule.vehiculeId === vehiculeSelectionne;

        return (
          <CircleMarker
            key={vehicule.vehiculeId}
            center={[vehicule.latitude, vehicule.longitude]}
            radius={selectionne ? 11 : 8}
            pathOptions={{
              color: '#ffffff',
              weight: selectionne ? 4 : 3,
              fillColor: statut.couleur,
              fillOpacity: 1,
            }}
            eventHandlers={{ click: () => onSelection(vehicule.vehiculeId) }}
          >
            <Tooltip direction="top" offset={[0, -10]}>
              {vehicule.vehiculeId} &middot; {statut.libelle}
            </Tooltip>

            <Popup>
              <div className="gt-popup">
                <strong>{vehicule.vehiculeId}</strong>
                <span className="gt-popup__statut" style={{ color: statut.couleur }}>
                  {statut.libelle}
                </span>
                <dl>
                  <dt>Vitesse</dt>
                  <dd>{Math.round(vehicule.vitesse)} km/h</dd>
                  <dt>Zone</dt>
                  <dd>{vehicule.zone}</dd>
                  <dt>Position</dt>
                  <dd>
                    {vehicule.latitude.toFixed(4)}, {vehicule.longitude.toFixed(4)}
                  </dd>
                  <dt>Mise a jour</dt>
                  <dd>il y a {ancienneteCourte(vehicule.horodatage, maintenant)}</dd>
                </dl>
                {vehicule.erreur && <p className="gt-popup__erreur">{vehicule.erreur}</p>}
              </div>
            </Popup>
          </CircleMarker>
        );
      })}
    </MapContainer>
  );
}
