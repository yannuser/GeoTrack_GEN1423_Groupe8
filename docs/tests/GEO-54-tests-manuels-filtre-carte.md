# GEO-54 — Tests Manuels Filtre Carte

## Informations
- **Ticket** : GEO-54
- **Story parente** : GEO-7 (Filtre carte)
- **Auteur** : Sory Fofana
- **Date** : 2026-08-05
- **Dépendances** : GEO-39 (critères), GEO-40 (maquette), GEO-53 (service C#)

---

## 1. Objectif

Valider manuellement le bon fonctionnement des filtres carte (véhicule, zone, alerte, temporel) dans l'interface utilisateur et via les endpoints API REST.

---

## 2. Prérequis

| # | Prérequis | Détail |
|---|-----------|--------|
| 1 | Base de données peuplée | Min. 20 véhicules avec positions GPS variées |
| 2 | Zones géographiques | Min. 3 zones définies (Gatineau, Ottawa, Montréal) |
| 3 | Alertes actives | Min. 5 alertes de sévérités variées (GEO-10) |
| 4 | API démarrée | `dotnet run` sur GeoTrack.Api |
| 5 | Navigateur | Chrome/Firefox dernière version |
| 6 | Outil API | Postman ou curl disponible |

---

## 3. Jeu de données de test

### 3.1 Véhicules

| ID | Plaque | Type | Statut | Zone | Vitesse | Conducteur |
|----|--------|------|--------|------|---------|------------|
| V001 | ABC-1234 | Camion | Actif | Gatineau | 45 km/h | Martin Dupont |
| V002 | DEF-5678 | Voiture | Actif | Ottawa | 62 km/h | Julie Tremblay |
| V003 | GHI-9012 | Camion | Inactif | Gatineau | 0 km/h | — |
| V004 | JKL-3456 | Van | Actif | Montréal | 88 km/h | Pierre Roy |
| V005 | MNO-7890 | Voiture | Maintenance | Ottawa | 0 km/h | — |
| V006 | PQR-1111 | Camion | Actif | Gatineau | 55 km/h | Luc Bernard |
| V007 | STU-2222 | Voiture | Actif | Ottawa | 72 km/h | Anne Gagnon |
| V008 | VWX-3333 | Van | Actif | Montréal | 40 km/h | Marc Leblanc |
| V009 | YZA-4444 | Camion | Actif | Gatineau | 95 km/h | David Chen |
| V010 | BCD-5555 | Voiture | Inactif | Ottawa | 0 km/h | — |

### 3.2 Zones géographiques

| Zone | Centre (lat, lng) | Rayon |
|------|-------------------|-------|
| Gatineau | 45.4765, -75.7013 | 15 km |
| Ottawa | 45.4215, -75.6972 | 12 km |
| Montréal | 45.5017, -73.5673 | 25 km |

### 3.3 Alertes actives

| ID Alerte | Véhicule | Sévérité | Type |
|-----------|----------|----------|------|
| A001 | V004 | Critique | Excès vitesse |
| A002 | V007 | Alerte | Excès vitesse |
| A003 | V009 | Critique | Excès vitesse |
| A004 | V002 | Avertissement | Excès vitesse |
| A005 | V006 | Avertissement | Sortie zone |

---

## 4. Cas de test — Filtre par véhicule

### TC-01 : Filtre par statut "Actif"

| Champ | Valeur |
|-------|--------|
| **Précondition** | Page carte ouverte, aucun filtre actif |
| **Action** | Cocher "Actif" dans le filtre Statut |
| **Résultat attendu** | Seuls V001, V002, V004, V006, V007, V008, V009 affichés (7 véhicules) |
| **Vérification** | Compteur = "7 / 10 véhicules" |

### TC-02 : Filtre par type "Camion"

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Cocher "Camion" dans le filtre Type |
| **Résultat attendu** | V001, V003, V006, V009 affichés (4 véhicules) |
| **Vérification** | Marqueurs camion uniquement sur la carte |

### TC-03 : Filtre combiné Statut + Type

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Cocher "Actif" + "Camion" |
| **Résultat attendu** | V001, V006, V009 affichés (3 véhicules — Actif AND Camion) |
| **Vérification** | Logique AND inter-catégories respectée |

### TC-04 : Filtre par recherche ID

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Taper "ABC" dans la barre de recherche |
| **Résultat attendu** | V001 (ABC-1234) affiché uniquement |
| **Vérification** | Recherche partielle fonctionne |

### TC-05 : Filtre par conducteur

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Taper "Martin" dans le champ conducteur |
| **Résultat attendu** | V001 (Martin Dupont) affiché |
| **Vérification** | Recherche insensible à la casse |

### TC-06 : Filtre multiple statuts (OR intra-catégorie)

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Cocher "Actif" + "Inactif" dans Statut |
| **Résultat attendu** | V001-V004, V006-V010 affichés (9 véhicules — exclut Maintenance) |
| **Vérification** | Logique OR intra-catégorie respectée |

---

## 5. Cas de test — Filtre par zone

### TC-07 : Filtre zone "Gatineau"

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Sélectionner "Gatineau" dans le dropdown zone |
| **Résultat attendu** | V001, V003, V006, V009 affichés (4 véhicules) |
| **Vérification** | Carte centrée sur Gatineau |

### TC-08 : Filtre rayon personnalisé

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Cliquer un point sur la carte + rayon 5 km |
| **Résultat attendu** | Seuls véhicules dans le rayon de 5 km affichés |
| **Vérification** | Cercle de rayon visible sur la carte, calcul Haversine correct |

### TC-09 : Filtre zone + statut (AND inter-catégories)

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Zone "Gatineau" + Statut "Actif" |
| **Résultat attendu** | V001, V006, V009 affichés (3 véhicules — Gatineau AND Actif) |
| **Vérification** | V003 exclu (Inactif) |

---

## 6. Cas de test — Filtre par alerte

### TC-10 : Filtre alertes actives uniquement

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Activer toggle "Alertes actives" |
| **Résultat attendu** | V002, V004, V006, V007, V009 affichés (5 véhicules avec alertes) |
| **Vérification** | Marqueurs rouges/orange sur la carte |

### TC-11 : Filtre sévérité "Critique"

| Champ | Valeur |
|-------|--------|
| **Précondition** | Toggle alertes actif |
| **Action** | Cocher uniquement "Critique" |
| **Résultat attendu** | V004, V009 affichés (2 véhicules) |
| **Vérification** | Marqueurs rouges uniquement |

### TC-12 : Filtre alerte + zone (AND)

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Alertes actives + Zone "Gatineau" |
| **Résultat attendu** | V006 (Avertissement), V009 (Critique) affichés |
| **Vérification** | Intersection correcte alerte ∩ zone |

---

## 7. Cas de test — Filtre temporel

### TC-13 : Filtre dernière heure

| Champ | Valeur |
|-------|--------|
| **Précondition** | Aucun filtre actif |
| **Action** | Sélectionner "Dernière heure" dans le filtre temporel |
| **Résultat attendu** | Seuls véhicules actifs dans la dernière heure affichés |
| **Vérification** | Véhicules inactifs depuis > 1h masqués |

### TC-14 : Historique trajet véhicule

| Champ | Valeur |
|-------|--------|
| **Précondition** | Un véhicule sélectionné (V001) |
| **Action** | Activer "Historique trajet" + période "Aujourd'hui" |
| **Résultat attendu** | Polyline du trajet affiché sur la carte |
| **Vérification** | Points GPS reliés chronologiquement |

---

## 8. Cas de test — Performance et contraintes

### TC-15 : Limite 500 véhicules

| Champ | Valeur |
|-------|--------|
| **Précondition** | Base avec 600 véhicules |
| **Action** | Aucun filtre (afficher tous) |
| **Résultat attendu** | Max 500 véhicules affichés + message "Limite atteinte, affinez vos filtres" |
| **Vérification** | Compteur = "500 / 600 véhicules (limite)" |

### TC-16 : Refresh automatique 5 secondes

| Champ | Valeur |
|-------|--------|
| **Précondition** | Filtres appliqués, véhicules affichés |
| **Action** | Attendre 5 secondes sans action |
| **Résultat attendu** | Positions mises à jour automatiquement (marqueurs bougent) |
| **Vérification** | Pas de clignotement, transition fluide |

### TC-17 : Timeout API 3 secondes

| Champ | Valeur |
|-------|--------|
| **Précondition** | API simulée avec latence > 3s |
| **Action** | Appliquer un filtre |
| **Résultat attendu** | Message "Délai dépassé, veuillez réessayer" après 3s |
| **Vérification** | UI ne reste pas bloquée |

---

## 9. Cas de test — Réinitialisation

### TC-18 : Bouton "Réinitialiser tous les filtres"

| Champ | Valeur |
|-------|--------|
| **Précondition** | Multiples filtres actifs |
| **Action** | Cliquer "Réinitialiser" |
| **Résultat attendu** | Tous filtres désactivés, tous véhicules affichés |
| **Vérification** | Compteur revient à "X / X véhicules" |

### TC-19 : Retirer un filtre individuel (tag X)

| Champ | Valeur |
|-------|--------|
| **Précondition** | Filtre Zone + Statut actifs |
| **Action** | Cliquer X sur le tag "Gatineau" |
| **Résultat attendu** | Filtre zone retiré, filtre statut reste |
| **Vérification** | Résultats recalculés immédiatement |

---

## 10. Cas de test — API REST (Postman/curl)

### TC-20 : GET /api/filtrecarte/vehicules (sans filtre)

```bash
curl -X GET http://localhost:5000/api/filtrecarte/vehicules
```

| Résultat attendu | Tous les véhicules retournés (max 500) |
|------------------|----------------------------------------|

### TC-21 : POST /api/filtrecarte/appliquer

```bash
curl -X POST http://localhost:5000/api/filtrecarte/appliquer \
  -H "Content-Type: application/json" \
  -d '{
    "statuts": ["Actif"],
    "types": ["Camion"],
    "zoneId": null,
    "alertesActives": false
  }'
```

| Résultat attendu | V001, V006, V009 retournés (3 véhicules Actif + Camion) |
|------------------|----------------------------------------------------------|

### TC-22 : GET /api/filtrecarte/zones/{zoneId}/vehicules

```bash
curl -X GET http://localhost:5000/api/filtrecarte/zones/gatineau/vehicules
```

| Résultat attendu | V001, V003, V006, V009 retournés |
|------------------|----------------------------------|

---

## 11. Résumé des résultats

| # Test | Description | Résultat | Notes |
|--------|-------------|----------|-------|
| TC-01 | Filtre statut Actif | ⬜ PASS / ⬜ FAIL | |
| TC-02 | Filtre type Camion | ⬜ PASS / ⬜ FAIL | |
| TC-03 | Combiné Statut + Type | ⬜ PASS / ⬜ FAIL | |
| TC-04 | Recherche ID | ⬜ PASS / ⬜ FAIL | |
| TC-05 | Recherche conducteur | ⬜ PASS / ⬜ FAIL | |
| TC-06 | Multiple statuts (OR) | ⬜ PASS / ⬜ FAIL | |
| TC-07 | Filtre zone Gatineau | ⬜ PASS / ⬜ FAIL | |
| TC-08 | Rayon personnalisé | ⬜ PASS / ⬜ FAIL | |
| TC-09 | Zone + Statut (AND) | ⬜ PASS / ⬜ FAIL | |
| TC-10 | Alertes actives | ⬜ PASS / ⬜ FAIL | |
| TC-11 | Sévérité Critique | ⬜ PASS / ⬜ FAIL | |
| TC-12 | Alerte + Zone | ⬜ PASS / ⬜ FAIL | |
| TC-13 | Dernière heure | ⬜ PASS / ⬜ FAIL | |
| TC-14 | Historique trajet | ⬜ PASS / ⬜ FAIL | |
| TC-15 | Limite 500 | ⬜ PASS / ⬜ FAIL | |
| TC-16 | Refresh 5s | ⬜ PASS / ⬜ FAIL | |
| TC-17 | Timeout 3s | ⬜ PASS / ⬜ FAIL | |
| TC-18 | Réinitialiser tous | ⬜ PASS / ⬜ FAIL | |
| TC-19 | Retirer filtre individuel | ⬜ PASS / ⬜ FAIL | |
| TC-20 | API GET sans filtre | ⬜ PASS / ⬜ FAIL | |
| TC-21 | API POST appliquer | ⬜ PASS / ⬜ FAIL | |
| TC-22 | API GET zone | ⬜ PASS / ⬜ FAIL | |

---

## 12. Critères d'acceptation

- ✅ **22/22 tests PASS** pour valider GEO-54
- ✅ Logique AND inter-catégories vérifiée (TC-03, TC-09, TC-12)
- ✅ Logique OR intra-catégorie vérifiée (TC-06)
- ✅ Performance : refresh 5s, timeout 3s, limite 500 (TC-15/16/17)
- ✅ Intégration alertes GEO-10 fonctionnelle (TC-10/11/12)
- ✅ API REST conforme aux 6 endpoints documentés dans GEO-53

---

## 13. Environnement de test

| Élément | Version |
|---------|---------|
| OS | Windows Server 2022 |
| .NET | 8.0 |
| Navigateur | Chrome 125+ |
| Base de données | SQL Server LocalDB |
| Outil API | Postman v11 / curl |
