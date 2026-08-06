import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert } from 'react-bootstrap';
import { estNonAutorise, obtenirPositions } from './api';
import { effacerSession, lireSession, type SessionUtilisateur } from './auth';
import { BarreFiltres, TOUTES_ZONES } from './components/BarreFiltres';
import { CarteVehicules } from './components/CarteVehicules';
import { EnTete } from './components/EnTete';
import { FormulaireConnexion } from './components/FormulaireConnexion';
import { PanneauVehicules } from './components/PanneauVehicules';
import { ORDRE_STATUTS, versVehicules, type StatutVehicule, type Vehicule } from './types';

/** Cadence de rafraichissement, conforme au diagramme de sequence GEO-25. */
const INTERVALLE_RAFRAICHISSEMENT_MS = 5_000;

/**
 * GEO-18 : porte d'entree de l'application.
 * Sans session valide, seul l'ecran de connexion est monte — le suivi de flotte
 * n'est donc jamais rendu, et aucun appel a l'API protegee n'est tente.
 */
export default function App() {
  const [session, setSession] = useState<SessionUtilisateur | null>(() => lireSession());
  const [messageSession, setMessageSession] = useState<string | null>(null);

  const deconnecter = useCallback((message: string | null) => {
    effacerSession();
    setMessageSession(message);
    setSession(null);
  }, []);

  if (!session) {
    return (
      <FormulaireConnexion
        onConnecte={(nouvelle) => {
          setMessageSession(null);
          setSession(nouvelle);
        }}
        messageInitial={messageSession}
      />
    );
  }

  return (
    <EcranFlotte
      // Remonter le composant a chaque session evite de conserver l'etat
      // (vehicules, filtres) d'un utilisateur precedent.
      key={session.jeton}
      session={session}
      onDeconnexion={deconnecter}
    />
  );
}

interface ProprietesEcranFlotte {
  session: SessionUtilisateur;
  onDeconnexion: (message: string | null) => void;
}

function EcranFlotte({ session, onDeconnexion }: ProprietesEcranFlotte) {
  const [vehicules, setVehicules] = useState<Vehicule[]>([]);
  const [erreur, setErreur] = useState<string | null>(null);
  const [enChargement, setEnChargement] = useState(false);
  const [derniereMaj, setDerniereMaj] = useState<Date | null>(null);

  const [recherche, setRecherche] = useState('');
  const [zoneSelectionnee, setZoneSelectionnee] = useState<string>(TOUTES_ZONES);
  const [statutsActifs, setStatutsActifs] = useState<Set<StatutVehicule>>(
    () => new Set(ORDRE_STATUTS),
  );
  const [vehiculeSelectionne, setVehiculeSelectionne] = useState<string | null>(null);

  // Horloge dediee aux libelles "il y a X", pour qu'ils avancent entre deux appels.
  const [maintenant, setMaintenant] = useState(() => new Date());

  const requeteEnCours = useRef<AbortController | null>(null);

  const charger = useCallback(async () => {
    requeteEnCours.current?.abort();
    const controleur = new AbortController();
    requeteEnCours.current = controleur;

    setEnChargement(true);
    try {
      const positions = await obtenirPositions(session.jeton, controleur.signal);
      setVehicules(versVehicules(positions));
      setDerniereMaj(new Date());
      setErreur(null);
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === 'AbortError') return;

      // Jeton refuse ou expire : on renvoie l'utilisateur vers la connexion
      // plutot que d'afficher une carte vide et de reinterroger toutes les 5 s.
      if (estNonAutorise(cause)) {
        onDeconnexion(cause instanceof Error ? cause.message : null);
        return;
      }

      setErreur(cause instanceof Error ? cause.message : 'Erreur inconnue.');
    } finally {
      if (requeteEnCours.current === controleur) setEnChargement(false);
    }
  }, [session.jeton, onDeconnexion]);

  // Chargement initial + rafraichissement automatique toutes les 5 secondes.
  useEffect(() => {
    void charger();
    const minuterie = setInterval(() => void charger(), INTERVALLE_RAFRAICHISSEMENT_MS);

    return () => {
      clearInterval(minuterie);
      requeteEnCours.current?.abort();
    };
  }, [charger]);

  // Tic d'une seconde : fait vivre les compteurs d'anciennete.
  useEffect(() => {
    const tic = setInterval(() => setMaintenant(new Date()), 1_000);
    return () => clearInterval(tic);
  }, []);

  const compteurs = useMemo(() => {
    const total: Record<StatutVehicule, number> = { en_route: 0, a_l_arret: 0, panne: 0 };
    for (const vehicule of vehicules) total[vehicule.statut] += 1;
    return total;
  }, [vehicules]);

  const zones = useMemo(
    () => [...new Set(vehicules.map((vehicule) => vehicule.zone))].sort(),
    [vehicules],
  );

  const vehiculesFiltres = useMemo(() => {
    const terme = recherche.trim().toLowerCase();

    return vehicules.filter((vehicule) => {
      if (!statutsActifs.has(vehicule.statut)) return false;
      if (zoneSelectionnee !== TOUTES_ZONES && vehicule.zone !== zoneSelectionnee) return false;
      if (
        terme &&
        !vehicule.vehiculeId.toLowerCase().includes(terme) &&
        !vehicule.nomAffiche.toLowerCase().includes(terme)
      ) {
        return false;
      }
      return true;
    });
  }, [vehicules, recherche, zoneSelectionnee, statutsActifs]);

  const basculerStatut = useCallback((statut: StatutVehicule) => {
    setStatutsActifs((precedents) => {
      const suivants = new Set(precedents);
      if (suivants.has(statut)) {
        suivants.delete(statut);
      } else {
        suivants.add(statut);
      }
      // Ne jamais tout masquer : le dernier filtre actif reste actif.
      return suivants.size === 0 ? precedents : suivants;
    });
  }, []);

  const secondesDepuisMaj = derniereMaj
    ? Math.max(0, Math.floor((maintenant.getTime() - derniereMaj.getTime()) / 1000))
    : null;

  return (
    <div className="gt-app">
      <EnTete
        compteurs={compteurs}
        session={session}
        onDeconnexion={() => onDeconnexion(null)}
      />

      <div className="gt-corps">
        <PanneauVehicules
          vehicules={vehiculesFiltres}
          vehiculeSelectionne={vehiculeSelectionne}
          onSelection={setVehiculeSelectionne}
          maintenant={maintenant}
        />

        <main className="gt-principal">
          <BarreFiltres
            recherche={recherche}
            onRecherche={setRecherche}
            zones={zones}
            zoneSelectionnee={zoneSelectionnee}
            onZone={setZoneSelectionnee}
            statutsActifs={statutsActifs}
            onBasculerStatut={basculerStatut}
            compteurs={compteurs}
            secondesDepuisMaj={secondesDepuisMaj}
            enChargement={enChargement}
          />

          {erreur && (
            <Alert variant="danger" className="gt-alerte">
              {erreur}
            </Alert>
          )}

          <CarteVehicules
            vehicules={vehiculesFiltres}
            vehiculeSelectionne={vehiculeSelectionne}
            onSelection={setVehiculeSelectionne}
            maintenant={maintenant}
          />
        </main>
      </div>
    </div>
  );
}
