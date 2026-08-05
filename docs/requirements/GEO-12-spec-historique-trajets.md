# GEO-12 — Spécification : Historique des trajets

## 1. Story

> En tant que gestionnaire de flotte, je souhaite consulter l'historique complet des trajets d'un véhicule afin d'analyser son utilisation.

## 2. Critères d'acceptation

| # | Critère | Validation |
|---|---------|-----------|
| 1 | L'historique affiche les trajets avec date et durée | Liste triée par date, durée calculée automatiquement |
| 2 | Le trajet peut être visualisé sur une carte | Tracé polyline sur carte Leaflet |

## 3. Epic parent

GEO-3 — Historique et tableau de bord analytique

## 4. Modèle de données

### 4.1 Entité Trajet

| Champ | Type | Description |
|-------|------|-------------|
| Id | Guid | Identifiant unique du trajet |
| VehiculeId | Guid | FK vers le véhicule |
| DateDebut | DateTime | Horodatage début du trajet |
| DateFin | DateTime | Horodatage fin du trajet |
| Duree | TimeSpan | Calculé : DateFin - DateDebut |
| DistanceKm | double | Distance totale parcourue en km |
| VitesseMoyenneKmH | double | Vitesse moyenne sur le trajet |
| VitesseMaxKmH | double | Vitesse maximale atteinte |
| PointDepart | Coordonnee | Lat/Lng du point de départ |
| PointArrivee | Coordonnee | Lat/Lng du point d'arrivée |
| AdresseDepart | string | Géocodage inverse du départ |
| AdresseArrivee | string | Géocodage inverse de l'arrivée |
| Statut | StatutTrajet | EnCours, Termine, Incomplet |

### 4.2 Entité PointGps

| Champ | Type | Description |
|-------|------|-------------|
| Id | Guid | Identifiant unique |
| TrajetId | Guid | FK vers le trajet |
| Latitude | double | Latitude GPS |
| Longitude | double | Longitude GPS |
| Horodatage | DateTime | Timestamp du point |
| VitesseKmH | double | Vitesse instantanée |
| Cap | double | Direction en degrés (0-360) |
| Ordre | int | Ordre séquentiel dans le trajet |

### 4.3 Enums

```csharp
public enum StatutTrajet
{
    EnCours,
    Termine,
    Incomplet  // Signal GPS perdu
}
```

## 5. Règles métier

| Règle | Description |
|-------|-------------|
| Détection début trajet | Vitesse > 5 km/h pendant ≥ 30 secondes |
| Détection fin trajet | Vitesse < 5 km/h pendant ≥ 5 minutes |
| Trajet incomplet | Perte signal GPS > 10 minutes pendant un trajet |
| Durée minimum | Un trajet doit durer ≥ 1 minute pour être enregistré |
| Distance minimum | Un trajet doit parcourir ≥ 100 mètres |
| Fréquence GPS | 1 point toutes les 5 secondes (en mouvement) |
| Rétention données | Historique conservé 12 mois |
| Pagination | Max 50 trajets par page |

## 6. Endpoints API REST

### 6.1 Lister les trajets d'un véhicule

```
GET /api/vehicules/{vehiculeId}/trajets?page=1&taille=50&dateDebut=&dateFin=&tri=dateDesc
```

**Réponse** :
```json
{
  "trajets": [
    {
      "id": "guid",
      "dateDebut": "2026-08-05T08:30:00Z",
      "dateFin": "2026-08-05T09:15:00Z",
      "dureeMinutes": 45,
      "distanceKm": 32.5,
      "vitesseMoyenneKmH": 43.3,
      "adresseDepart": "123 Rue Principale, Gatineau",
      "adresseArrivee": "456 Boul. St-Joseph, Gatineau",
      "statut": "Termine"
    }
  ],
  "pagination": {
    "page": 1,
    "taille": 50,
    "total": 234
  }
}
```

### 6.2 Détail d'un trajet (avec points GPS pour la carte)

```
GET /api/trajets/{trajetId}
```

**Réponse** :
```json
{
  "id": "guid",
  "vehiculeId": "guid",
  "dateDebut": "2026-08-05T08:30:00Z",
  "dateFin": "2026-08-05T09:15:00Z",
  "dureeMinutes": 45,
  "distanceKm": 32.5,
  "vitesseMoyenneKmH": 43.3,
  "vitesseMaxKmH": 78.0,
  "pointDepart": { "lat": 45.4765, "lng": -75.7013 },
  "pointArrivee": { "lat": 45.4283, "lng": -75.7507 },
  "adresseDepart": "123 Rue Principale, Gatineau",
  "adresseArrivee": "456 Boul. St-Joseph, Gatineau",
  "statut": "Termine",
  "pointsGps": [
    { "lat": 45.4765, "lng": -75.7013, "horodatage": "2026-08-05T08:30:00Z", "vitesseKmH": 12.0, "cap": 180 },
    { "lat": 45.4760, "lng": -75.7020, "horodatage": "2026-08-05T08:30:05Z", "vitesseKmH": 35.0, "cap": 195 }
  ]
}
```

### 6.3 Statistiques globales d'un véhicule

```
GET /api/vehicules/{vehiculeId}/trajets/statistiques?periode=30j
```

**Réponse** :
```json
{
  "periode": "30j",
  "nombreTrajets": 87,
  "distanceTotaleKm": 2450.3,
  "dureeTotaleHeures": 62.5,
  "vitesseMoyenneKmH": 39.2,
  "trajetLePlusLong": { "id": "guid", "distanceKm": 120.5 },
  "trajetLePlusCourt": { "id": "guid", "distanceKm": 0.8 }
}
```

### 6.4 Exporter les trajets (CSV)

```
GET /api/vehicules/{vehiculeId}/trajets/export?format=csv&dateDebut=&dateFin=
```

## 7. Interface utilisateur

### 7.1 Page historique (Critère #1)

| Élément | Description |
|---------|-------------|
| Sélecteur véhicule | Dropdown avec recherche |
| Filtre période | DatePicker début/fin |
| Tableau trajets | Colonnes : Date, Départ → Arrivée, Durée, Distance, Vitesse moy., Statut |
| Tri | Par date (défaut desc), durée, distance |
| Pagination | 50 par page, navigation bas de tableau |
| Badge statut | Vert=Terminé, Orange=EnCours, Rouge=Incomplet |
| Export | Bouton CSV |

### 7.2 Vue carte (Critère #2)

| Élément | Description |
|---------|-------------|
| Carte Leaflet | Tracé polyline du trajet sélectionné |
| Marqueur départ | Icône verte (point A) |
| Marqueur arrivée | Icône rouge (point B) |
| Couleur tracé | Gradient vitesse (vert=lent → rouge=rapide) |
| Tooltip points | Hover → horodatage + vitesse |
| Contrôles | Zoom, recentrer, animation play/pause |
| Panneau info | Résumé trajet (durée, distance, vitesse) |

## 8. Intégrations

| Story | Lien |
|-------|------|
| GEO-7 | Filtre carte — réutilise le composant carte |
| GEO-9 | Alertes — si un trajet déclenche une alerte vitesse |
| GEO-15 | Ajout véhicule — les trajets sont liés au véhicule ajouté |

## 9. Contraintes techniques

| Contrainte | Valeur |
|-----------|--------|
| Max points GPS par trajet | 10 000 (simplification Douglas-Peucker si > 10 000) |
| Temps de réponse liste | < 500ms |
| Temps de réponse carte | < 2s (avec 10 000 points) |
| Format dates | ISO 8601, fuseau America/Toronto |
| Coordonnées | WGS84 (decimal degrees) |

## 10. Flux utilisateur

```
Gestionnaire → Sélectionne véhicule → Voit liste trajets (Critère #1)
                                        ↓
                               Clique un trajet → Voit tracé sur carte (Critère #2)
                                        ↓
                               Peut exporter en CSV
```
