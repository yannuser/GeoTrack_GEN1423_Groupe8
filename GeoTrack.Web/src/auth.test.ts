import { describe, expect, it } from 'vitest';
import { effacerSession, enregistrerSession, initiales, lireSession } from './auth';
import { CLE_SESSION, sessionValide } from './test/fixtures';

describe('auth - persistance de la session', () => {
  it('relit une session valide enregistree', () => {
    const session = sessionValide();
    enregistrerSession(session);

    expect(lireSession()).toEqual(session);
  });

  it('retourne null quand aucune session n est stockee', () => {
    expect(lireSession()).toBeNull();
  });

  it('ignore et purge une session expiree', () => {
    enregistrerSession({
      ...sessionValide(),
      expiration: new Date(Date.now() - 1_000).toISOString(),
    });

    expect(lireSession()).toBeNull();
    expect(localStorage.getItem(CLE_SESSION)).toBeNull();
  });

  it('ignore et purge un contenu illisible', () => {
    localStorage.setItem(CLE_SESSION, 'ceci-nest-pas-du-json');

    expect(lireSession()).toBeNull();
    expect(localStorage.getItem(CLE_SESSION)).toBeNull();
  });

  it('ignore une session sans jeton', () => {
    localStorage.setItem(CLE_SESSION, JSON.stringify({ ...sessionValide(), jeton: '' }));

    expect(lireSession()).toBeNull();
  });

  it('effacerSession supprime la cle', () => {
    enregistrerSession(sessionValide());
    effacerSession();

    expect(localStorage.getItem(CLE_SESSION)).toBeNull();
    expect(lireSession()).toBeNull();
  });
});

describe('auth - initiales', () => {
  it.each([
    ['Jean Dubois', 'jean.dubois', 'JD'],
    ['Marie-Claire Tremblay', 'mct', 'MT'],
    ['Cher', 'cher', 'C'],
  ])('%s -> %s', (nomComplet, identifiant, attendu) => {
    expect(initiales(nomComplet, identifiant)).toBe(attendu);
  });

  it('se rabat sur l identifiant quand le nom est vide', () => {
    expect(initiales('', 'jean.dubois')).toBe('JD');
  });

  it('retourne un repli quand tout est vide', () => {
    expect(initiales('', '')).toBe('?');
  });
});
