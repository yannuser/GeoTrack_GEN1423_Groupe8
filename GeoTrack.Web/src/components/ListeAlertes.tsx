import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Badge, Spinner } from 'react-bootstrap';
import { estNonAutorise, obtenirAlertes } from '../api';
import {
  formaterDateAlerte,
  infoSeverite,
  libelleTypeAlerte,
  type Alerte as AlerteApi,
} from '../types';

interface Props {
  jeton: string;
  /**
   * Jeton refuse ou expire : remonte au parent, qui reutilise le meme
   * mecanisme de deconnexion que la vue carte (GEO-18).
   */
  onNonAutorise: (message: string | null) => void;
}

/**
 * GEO-59 : historique des alertes (GET /api/alertes).
 *
 * Chargement unique au montage : contrairement aux positions, une alerte est un
 * evenement passe et immuable — rien ne justifie un rafraichissement toutes les
 * 5 secondes. Un bouton permet de recharger a la demande.
 *
 * L'ordre est celui renvoye par l'API (date decroissante) : aucun tri client,
 * pour ne pas risquer de diverger du contrat serveur.
 */
export function ListeAlertes({ jeton, onNonAutorise }: Props) {
  const [alertes, setAlertes] = useState<AlerteApi[]>([]);
  const [enChargement, setEnChargement] = useState(true);
  const [erreur, setErreur] = useState<string | null>(null);

  const requeteEnCours = useRef<AbortController | null>(null);

  const charger = useCallback(async () => {
    requeteEnCours.current?.abort();
    const controleur = new AbortController();
    requeteEnCours.current = controleur;

    setEnChargement(true);
    try {
      const recues = await obtenirAlertes(jeton, undefined, controleur.signal);
      setAlertes(recues);
      setErreur(null);
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === 'AbortError') return;

      if (estNonAutorise(cause)) {
        onNonAutorise(cause instanceof Error ? cause.message : null);
        return;
      }

      setErreur(cause instanceof Error ? cause.message : 'Erreur inconnue.');
    } finally {
      if (requeteEnCours.current === controleur) setEnChargement(false);
    }
  }, [jeton, onNonAutorise]);

  useEffect(() => {
    void charger();
    return () => requeteEnCours.current?.abort();
  }, [charger]);

  return (
    <section className="gt-alertes" aria-labelledby="gt-alertes-titre">
      <div className="gt-alertes__entete">
        <h2 className="gt-alertes__titre" id="gt-alertes-titre">
          HISTORIQUE DES ALERTES
        </h2>

        <div className="gt-alertes__actions">
          {!enChargement && (
            <span className="gt-alertes__compteur">
              <strong>{alertes.length}</strong> {alertes.length > 1 ? 'alertes' : 'alerte'}
            </span>
          )}
          <button
            type="button"
            className="gt-alertes__recharger"
            onClick={() => void charger()}
            disabled={enChargement}
          >
            Recharger
          </button>
        </div>
      </div>

      {erreur && (
        <Alert variant="danger" className="gt-alerte">
          {erreur}
        </Alert>
      )}

      {enChargement && (
        <p className="gt-alertes__etat" role="status">
          <Spinner animation="border" size="sm" className="me-2" />
          Chargement des alertes...
        </p>
      )}

      {!enChargement && !erreur && alertes.length === 0 && (
        <p className="gt-alertes__etat gt-alertes__etat--vide">Aucune alerte pour le moment</p>
      )}

      {!enChargement && alertes.length > 0 && (
        <div className="gt-alertes__tableau-cadre">
          <table className="gt-alertes__tableau">
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">Vehicule</th>
                <th scope="col">Type</th>
                <th scope="col">Severite</th>
                <th scope="col">Details</th>
              </tr>
            </thead>
            <tbody>
              {alertes.map((alerte) => {
                const severite = infoSeverite(alerte.severite);

                return (
                  <tr key={alerte.id}>
                    <td className="gt-alertes__date">{formaterDateAlerte(alerte.date)}</td>
                    <td className="gt-alertes__vehicule">{alerte.vehiculeId}</td>
                    <td>{libelleTypeAlerte(alerte.typeAlerte)}</td>
                    <td>
                      <span
                        className="gt-point-mini"
                        style={{ background: severite.couleur }}
                        aria-hidden="true"
                      />
                      <Badge bg={severite.variantBadge} pill className="gt-badge">
                        {severite.libelle}
                      </Badge>
                    </td>
                    <td className="gt-alertes__details">{alerte.details}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
