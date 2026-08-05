# GEO-32 — Analyse des besoins : Définition d'une zone géographique (Geofencing)

**Projet** : GeoTrack — GEN1423 Génie logiciel, Groupe 8  
**Auteur** : Sory Fofana  
**Date** : 2026-08-04  
**Ticket Jira** : GEO-32  
**Story parente** : GEO-9 — Zone géographique  
**Statut** : Terminé  

---

## 1. Définition du geofencing

Le **geofencing** (ou géorepérage) est une fonctionnalité qui permet de définir une zone géographique virtuelle sur une carte, puis de détecter automatiquement lorsqu'un véhicule surveillé entre dans cette zone ou en sort.

Dans le contexte de GeoTrack :
- La flotte comprend **10 000 véhicules** transmettant leur position GPS toutes les **5 secondes**.
- Un gestionnaire peut définir une ou plusieurs zones polygonales ou circulaires sur la carte.
- Le système surveille en continu si un véhicule associé à une zone franchit sa frontière.
- Un événement d'alerte est généré à chaque entrée ou sortie détectée.

---

## 2. Acteur principal

| Acteur | Rôle |
|--------|------|
| **Gestionnaire de flotte** | Crée, modifie, supprime et surveille les zones géographiques. Associe des véhicules à une zone. |

Acteurs secondaires :
| Acteur | Rôle |
|--------|------|
| **Système de télémétrie** | Transmet les positions GPS des véhicules toutes les 5 secondes |
| **Service d'alerte** | Reçoit les événements de franchissement et notifie le gestionnaire |

---

## 3. Scénario nominal — Création d'une zone et détection de sortie

**Précondition** : Le gestionnaire est authentifié. Au moins un véhicule est enregistré dans le système.

| Étape | Acteur | Action |
|-------|--------|--------|
| 1 | Gestionnaire | Ouvre l'interface de gestion des zones géographiques |
| 2 | Gestionnaire | Clique sur « Créer une zone » |
| 3 | Système | Affiche la carte interactive |
| 4 | Gestionnaire | Dessine une zone polygonale ou circulaire sur la carte |
| 5 | Gestionnaire | Saisit un nom pour la zone (ex. : « Zone industrielle Nord ») |
| 6 | Gestionnaire | Sélectionne un ou plusieurs véhicules à surveiller |
| 7 | Gestionnaire | Active la surveillance et confirme |
| 8 | Système | Valide les données (nom non vide, zone géométriquement valide, véhicule existant) |
| 9 | Système | Persiste la zone et l'association zone-véhicule en base de données |
| 10 | Système | Confirme la création à l'interface |
| 11 | Service télémétrie | Transmet une nouvelle position GPS d'un véhicule surveillé |
| 12 | Service de zone | Calcule si la position est à l'intérieur ou à l'extérieur de la zone |
| 13 | Service de zone | Détecte un franchissement (entrée ou sortie) |
| 14 | Service d'alerte | Génère et enregistre un événement de franchissement |
| 15 | Système | Notifie le gestionnaire (interface ou notification) |

---

## 4. Scénarios alternatifs

### 4.1 — Zone géométriquement invalide
- **Déclencheur** : Le gestionnaire dessine une zone avec moins de 3 points (polygone) ou un rayon nul (cercle).
- **Résultat** : Le système affiche un message d'erreur : *« La zone doit contenir au moins 3 points distincts. »* La zone n'est pas sauvegardée.

### 4.2 — Nom de zone déjà utilisé
- **Déclencheur** : Le gestionnaire saisit un nom identique à une zone existante.
- **Résultat** : Le système affiche : *« Ce nom de zone est déjà utilisé. Veuillez en choisir un autre. »*

### 4.3 — Aucun véhicule sélectionné
- **Déclencheur** : Le gestionnaire crée une zone sans associer de véhicule.
- **Résultat** : La zone est créée mais la surveillance est inactive. Un avertissement est affiché : *« Aucun véhicule associé. La surveillance ne sera pas active. »*

### 4.4 — Véhicule déjà à l'intérieur de la zone lors de l'activation
- **Déclencheur** : Au moment de l'activation, la dernière position connue du véhicule est déjà dans la zone.
- **Résultat** : Le système enregistre l'état initial comme « à l'intérieur » sans générer d'alerte d'entrée. Une alerte sera générée uniquement lors de la prochaine sortie.

### 4.5 — Position GPS manquante ou corrompue
- **Déclencheur** : Le service de télémétrie transmet une position avec des coordonnées nulles ou hors plage valide.
- **Résultat** : Le service de zone ignore cette position et journalise un avertissement. Aucune alerte n'est générée.

### 4.6 — Surveillance désactivée manuellement
- **Déclencheur** : Le gestionnaire désactive la surveillance d'une zone.
- **Résultat** : Le service de zone cesse de traiter les positions pour cette zone. Les alertes en cours sont clôturées.

---

## 5. Données nécessaires

### 5.1 — Entité Zone (GeofenceZone)

| Champ | Type | Contrainte | Description |
|-------|------|------------|-------------|
| `id` | UUID | PK, auto-généré | Identifiant unique de la zone |
| `name` | String | Non nul, unique, max 100 chars | Nom de la zone |
| `type` | Enum | POLYGON \| CIRCLE | Type de géométrie |
| `coordinates` | List\<GeoPoint\> | Min 3 points si POLYGON | Sommets du polygone |
| `center` | GeoPoint | Requis si CIRCLE | Centre du cercle |
| `radiusMeters` | Double | > 0 si CIRCLE | Rayon en mètres |
| `active` | Boolean | Non nul | Surveillance active ou non |
| `createdAt` | LocalDateTime | Auto | Date de création |
| `updatedAt` | LocalDateTime | Auto | Date de dernière modification |

### 5.2 — Entité Association Zone-Véhicule (ZoneVehicleAssociation)

| Champ | Type | Contrainte | Description |
|-------|------|------------|-------------|
| `id` | UUID | PK | Identifiant |
| `zoneId` | UUID | FK → GeofenceZone | Zone concernée |
| `vehicleId` | UUID | FK → Vehicle | Véhicule surveillé |
| `lastKnownState` | Enum | INSIDE \| OUTSIDE \| UNKNOWN | Dernier état connu |
| `assignedAt` | LocalDateTime | Auto | Date d'association |

### 5.3 — Entité Événement de franchissement (GeofenceEvent)

| Champ | Type | Contrainte | Description |
|-------|------|------------|-------------|
| `id` | UUID | PK | Identifiant |
| `zoneId` | UUID | FK | Zone concernée |
| `vehicleId` | UUID | FK | Véhicule concerné |
| `eventType` | Enum | ENTRY \| EXIT | Type d'événement |
| `latitude` | Double | [-90, 90] | Latitude au moment du franchissement |
| `longitude` | Double | [-180, 180] | Longitude au moment du franchissement |
| `occurredAt` | LocalDateTime | Non nul | Horodatage de l'événement |

### 5.4 — Entité Position GPS (GpsPosition) — partagée avec GEO-8 (Florian)

| Champ | Type | Description |
|-------|------|-------------|
| `vehicleId` | UUID | Identifiant du véhicule |
| `latitude` | Double | Latitude GPS |
| `longitude` | Double | Longitude GPS |
| `speed` | Double | Vitesse en km/h |
| `heading` | Double | Direction en degrés |
| `timestamp` | LocalDateTime | Horodatage de la position |

---

## 6. Règles métier

| ID | Règle |
|----|-------|
| RG-01 | Une zone doit avoir un nom unique dans le système. |
| RG-02 | Un polygone doit avoir au moins 3 sommets distincts et non colinéaires. |
| RG-03 | Un cercle doit avoir un rayon strictement positif (> 0 mètre). |
| RG-04 | Les coordonnées GPS doivent être dans les plages valides : latitude ∈ [-90, 90], longitude ∈ [-180, 180]. |
| RG-05 | Un véhicule peut être associé à plusieurs zones simultanément. |
| RG-06 | Une zone peut surveiller plusieurs véhicules simultanément. |
| RG-07 | Une alerte de franchissement n'est générée que si l'état change (INSIDE → OUTSIDE ou OUTSIDE → INSIDE). |
| RG-08 | Si l'état initial est inconnu (UNKNOWN), la première position valide établit l'état sans générer d'alerte. |
| RG-09 | Une zone inactive ne génère aucune alerte, même si un véhicule la franchit. |
| RG-10 | La suppression d'une zone supprime toutes ses associations et désactive ses alertes actives. |

---

## 7. Validations

| Champ | Validation |
|-------|------------|
| `name` | Non nul, non vide, longueur ≤ 100, unique en base |
| `type` | Valeur dans l'enum {POLYGON, CIRCLE} |
| `coordinates` | Si POLYGON : liste non nulle, taille ≥ 3 |
| `center` | Si CIRCLE : non nul |
| `radiusMeters` | Si CIRCLE : > 0 |
| `latitude` | ∈ [-90.0, 90.0] |
| `longitude` | ∈ [-180.0, 180.0] |
| `vehicleId` | Doit exister en base de données |

---

## 8. Erreurs et messages

| Code | Situation | Message affiché |
|------|-----------|-----------------|
| ERR-GEO-001 | Nom de zone vide | « Le nom de la zone est obligatoire. » |
| ERR-GEO-002 | Nom de zone déjà utilisé | « Ce nom de zone est déjà utilisé. » |
| ERR-GEO-003 | Polygone invalide (< 3 points) | « La zone doit contenir au moins 3 points distincts. » |
| ERR-GEO-004 | Rayon invalide (≤ 0) | « Le rayon doit être supérieur à zéro. » |
| ERR-GEO-005 | Coordonnées hors plage | « Les coordonnées GPS sont invalides. » |
| ERR-GEO-006 | Véhicule introuvable | « Le véhicule sélectionné n'existe pas dans le système. » |
| ERR-GEO-007 | Zone introuvable | « La zone demandée n'existe pas. » |

---

## 9. Critères d'acceptation mesurables

| ID | Critère | Méthode de vérification |
|----|---------|------------------------|
| CA-01 | Un gestionnaire peut créer une zone polygonale avec un nom, des coordonnées valides et un véhicule associé. | Test d'intégration : POST /api/zones → HTTP 201 |
| CA-02 | Un gestionnaire peut créer une zone circulaire avec centre et rayon. | Test d'intégration : POST /api/zones → HTTP 201 |
| CA-03 | La création échoue si le nom est vide ou déjà utilisé. | Test unitaire : validation → exception levée |
| CA-04 | La création échoue si le polygone a moins de 3 points. | Test unitaire : validation → exception levée |
| CA-05 | Le système détecte correctement qu'un véhicule est à l'intérieur d'une zone. | Test unitaire : GeofenceService.isInside() → true |
| CA-06 | Le système détecte correctement qu'un véhicule est à l'extérieur d'une zone. | Test unitaire : GeofenceService.isInside() → false |
| CA-07 | Un événement ENTRY est généré quand un véhicule passe de OUTSIDE à INSIDE. | Test unitaire : processPosition() → GeofenceEvent(ENTRY) |
| CA-08 | Un événement EXIT est généré quand un véhicule passe de INSIDE à OUTSIDE. | Test unitaire : processPosition() → GeofenceEvent(EXIT) |
| CA-09 | Aucun événement n'est généré si l'état ne change pas. | Test unitaire : processPosition() → aucun événement |
| CA-10 | Une position GPS invalide est ignorée sans générer d'alerte. | Test unitaire : coordonnées nulles → aucun événement |

---

## 10. Impacts architecturaux

### Nouveaux composants requis

| Composant | Type | Responsabilité |
|-----------|------|----------------|
| `GeofenceZone` | Entité JPA | Représente une zone géographique |
| `ZoneVehicleAssociation` | Entité JPA | Lie une zone à un véhicule |
| `GeofenceEvent` | Entité JPA | Enregistre un franchissement |
| `GeofenceService` | Service | Logique de détection entrée/sortie |
| `GeofenceController` | REST Controller | API CRUD pour les zones |
| `GeofenceRepository` | Repository JPA | Accès base de données |
| `GeofenceEventRepository` | Repository JPA | Accès événements |

### Dépendances avec d'autres composants

| Composant externe | Propriétaire | Nature de la dépendance |
|-------------------|-------------|------------------------|
| `GpsPosition` | Florian (GEO-8) | Le service de zone consomme les positions GPS |
| `Vehicle` | Sory (GEO-15) | L'association zone-véhicule référence l'entité Vehicle |
| `AlertService` | À définir | Reçoit les événements de franchissement |

### Algorithme de détection (Point-in-Polygon)

Pour les zones polygonales, l'algorithme **Ray Casting** sera utilisé :
- Lancer un rayon horizontal depuis le point testé
- Compter le nombre d'intersections avec les arêtes du polygone
- Si le nombre est impair → le point est à l'intérieur
- Complexité : O(n) où n = nombre de sommets

Pour les zones circulaires :
- Calculer la distance euclidienne (formule de Haversine pour la précision GPS)
- Si distance ≤ rayon → à l'intérieur

---

## 11. Risques

| ID | Risque | Probabilité | Impact | Mitigation |
|----|--------|-------------|--------|------------|
| R-01 | Format de `GpsPosition` de Florian (GEO-8) incompatible avec notre service | Moyen | Élevé | Utiliser un mock en attendant GEO-23 mergé |
| R-02 | Entité `Vehicle` non encore créée (GEO-15) | Élevé | Moyen | Utiliser un `vehicleId` UUID simple sans FK stricte pour le prototype |
| R-03 | Performance : 10 000 véhicules × toutes les 5 secondes = 2 000 positions/sec | Élevé | Élevé | Indexer sur `vehicleId` et `zoneId`; traitement asynchrone à prévoir |
| R-04 | Précision GPS insuffisante pour détecter les franchissements de frontière | Faible | Moyen | Ajouter une tolérance de ±10 mètres sur la frontière |
| R-05 | Alertes dupliquées si le véhicule oscille autour de la frontière | Moyen | Moyen | Mécanisme de debounce : ne changer d'état qu'après 2 positions consécutives cohérentes |

---

## 12. Hypothèses documentées

1. **Prototype** : Le système est un prototype universitaire. Les performances à 10 000 véhicules sont documentées comme architecture cible, pas implémentées.
2. **Authentification** : L'authentification (GEO-18, Florian) n'est pas encore disponible. Les endpoints seront non sécurisés pour le prototype.
3. **Format GPS** : On suppose que `GpsPosition` contient au minimum `vehicleId`, `latitude`, `longitude`, `timestamp`. À confirmer avec Florian.
4. **Persistance** : On utilisera une base H2 en mémoire pour le prototype (facilement remplaçable par PostgreSQL).

---

*Document produit dans le cadre du cours GEN1423 — Génie logiciel, Université du Québec en Outaouais, Groupe 8.*
