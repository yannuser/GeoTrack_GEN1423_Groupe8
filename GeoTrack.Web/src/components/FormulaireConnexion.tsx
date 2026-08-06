import { useId, useState, type FormEvent } from 'react';
import { Alert, Button, Form, Spinner } from 'react-bootstrap';
import { connexion } from '../api';
import { enregistrerSession, type SessionUtilisateur } from '../auth';

interface Props {
  onConnecte: (session: SessionUtilisateur) => void;
  /** Message affiche a l'arrivee (ex. session expiree apres un 401). */
  messageInitial?: string | null;
}

/** GEO-18 : ecran de connexion, seul point d'entree de l'application. */
export function FormulaireConnexion({ onConnecte, messageInitial = null }: Props) {
  const [identifiant, setIdentifiant] = useState('');
  const [motDePasse, setMotDePasse] = useState('');
  const [erreur, setErreur] = useState<string | null>(messageInitial);
  const [enCours, setEnCours] = useState(false);

  const idIdentifiant = useId();
  const idMotDePasse = useId();

  const champsRemplis = identifiant.trim() !== '' && motDePasse !== '';

  async function soumettre(evenement: FormEvent<HTMLFormElement>) {
    evenement.preventDefault();
    if (!champsRemplis || enCours) return;

    setEnCours(true);
    setErreur(null);

    try {
      const session = await connexion(identifiant.trim(), motDePasse);
      enregistrerSession(session);
      onConnecte(session);
    } catch (cause) {
      // Le message vient de l'API et reste volontairement generique.
      setErreur(cause instanceof Error ? cause.message : 'Connexion impossible.');
      setMotDePasse('');
    } finally {
      setEnCours(false);
    }
  }

  return (
    <div className="gt-connexion">
      <Form className="gt-connexion__carte" onSubmit={soumettre} noValidate>
        <div className="gt-connexion__marque">
          <span className="gt-logo" aria-hidden="true">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5A2.5 2.5 0 1 1 12 6.5a2.5 2.5 0 0 1 0 5z" />
            </svg>
          </span>
          <span className="gt-entete__titre">GeoTrack</span>
        </div>

        <h1 className="gt-connexion__titre">Connexion</h1>
        <p className="gt-connexion__soustitre">
          Identifiez-vous pour acceder au suivi de la flotte.
        </p>

        {erreur && (
          <Alert variant="danger" className="gt-alerte">
            {erreur}
          </Alert>
        )}

        <Form.Group className="gt-connexion__champ">
          <Form.Label htmlFor={idIdentifiant}>Identifiant</Form.Label>
          <Form.Control
            id={idIdentifiant}
            type="text"
            autoComplete="username"
            autoFocus
            value={identifiant}
            onChange={(evenement) => setIdentifiant(evenement.target.value)}
            disabled={enCours}
          />
        </Form.Group>

        <Form.Group className="gt-connexion__champ">
          <Form.Label htmlFor={idMotDePasse}>Mot de passe</Form.Label>
          <Form.Control
            id={idMotDePasse}
            type="password"
            autoComplete="current-password"
            value={motDePasse}
            onChange={(evenement) => setMotDePasse(evenement.target.value)}
            disabled={enCours}
          />
        </Form.Group>

        <Button type="submit" className="gt-connexion__bouton" disabled={!champsRemplis || enCours}>
          {enCours ? (
            <>
              <Spinner animation="border" size="sm" role="status" aria-hidden="true" />{' '}
              Connexion...
            </>
          ) : (
            'Se connecter'
          )}
        </Button>
      </Form>
    </div>
  );
}
