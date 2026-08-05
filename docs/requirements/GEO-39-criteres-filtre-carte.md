# GEO-39 — Définir les critères de filtre (par véhicule, par zone)

## 1. Contexte

**Story parente** : GEO-7 — Filtre carte  
**Epic** : GEO-1 — Suivi en temps réel des véhicules  
**Sprint** : GEO Sprint 1  

**User Story** :  
> En tant que gestionnaire de flotte, je souhaite voir la position de chaque véhicule sur une carte afin de connaître leur localisation en temps réel.

**Critères d'acceptation** :
1. La carte affiche tous les véhicules actifs
2. La position se met à jour en moins de quelques secondes

---

## 2. Objectif de GEO-39

Définir de manière exhaustive les critères de filtrage permettant à l'utilisateur de :
- Filtrer les véhicules affichés sur la carte
- Filtrer par zone géographique (intégration avec GEO-9)
- Combiner plusieurs filtres simultanément

---

## 3. Critères de filtre identifiés

### 3.1 Filtre par véhicule

| Critère | Type | Valeurs possibles | Défaut |
|---------|------|-------------------|--------|
| Identifiant véhicule | Texte libre (autocomplete) | ID ou immatriculation | Tous |
| Statut véhicule | Multi-sélection | Actif, Inactif, En maintenance, Hors service | Actif uniquement |
| Type véhicule | Multi-sélection | Camion, Voiture, Moto, Utilitaire | Tous |
| Groupe/Flotte | Multi-sélection | Flotte Nord, Flotte Sud, Flotte Est, Flotte Ouest | Tous |
| Conducteur assigné | Autocomplete | Liste des conducteurs | Tous |
| Vitesse actuelle | Plage numérique | 0 — 200 km/h (slider) | Aucun filtre |
| En mouvement | Booléen | Oui / Non / Tous | Tous |
| Dernière activité | Plage temporelle | Dernière 1h, 6h, 24h, 7j, Personnalisé | 24h |

### 3.2 Filtre par zone géographique

| Critère | Type | Valeurs possibles | Défaut |
|---------|------|-------------------|--------|
| Zone géographique | Multi-sélection | Zones définies dans GEO-9 | Toutes |
| Position relative | Sélection simple | Dans la zone, Hors zone, Tous | Tous |
| Rayon personnalisé | Numérique + point carte | 0.1 — 50 km autour d'un point | Désactivé |
| Proximité entre véhicules | Numérique | Distance min entre 2 véhicules (km) | Désactivé |

### 3.3 Filtre par alerte (intégration GEO-10)

| Critère | Type | Valeurs possibles | Défaut |
|---------|------|-------------------|--------|
| Alerte active | Booléen | Avec alerte / Sans alerte / Tous | Tous |
| Sévérité alerte | Multi-sélection | Avertissement, Alerte, Critique | Toutes |
| Type alerte | Multi-sélection | Vitesse, Geofencing, Inactivité | Tous |

### 3.4 Filtre temporel

| Critère | Type | Valeurs possibles | Défaut |
|---------|------|-------------------|--------|
| Période d'affichage | Sélection | Temps réel, Dernière heure, Aujourd'hui, Personnalisé | Temps réel |
| Historique trajet | Booléen | Afficher/Masquer le tracé | Masqué |

---

## 4. Logique de combinaison des filtres

### 4.1 Opérateurs logiques

```
Filtres intra-catégorie  → OU (OR)
Filtres inter-catégories → ET (AND)
```

**Exemple** :
- Type véhicule = [Camion, Utilitaire] → Camion **OU** Utilitaire
- Statut = Actif **ET** Zone = "Zone Nord" **ET** Type = [Camion, Utilitaire]

### 4.2 Priorité d'application

```
1. Filtre temporel (période)
2. Filtre statut véhicule
3. Filtre zone géographique
4. Filtre type/groupe
5. Filtre alerte
6. Filtre vitesse/mouvement
```

### 4.3 Résultat attendu

```
Véhicules affichés = TOUS les véhicules
    WHERE statut IN (filtres_statut)
    AND type IN (filtres_type)
    AND groupe IN (filtres_groupe)
    AND zone_actuelle IN (filtres_zone)
    AND vitesse BETWEEN (min, max)
    AND derniere_activite >= (seuil_temporel)
    AND (alerte_active = filtre_alerte OR filtre_alerte = 'Tous')
```

---

## 5. Comportements attendus

### 5.1 Mise à jour en temps réel

| Comportement | Description |
|--------------|-------------|
| Apparition | Un véhicule qui entre dans les critères apparaît sur la carte avec animation fade-in |
| Disparition | Un véhicule qui sort des critères disparaît avec animation fade-out |
| Compteur | Le nombre de véhicules affichés / total est mis à jour en continu |
| Latence | Mise à jour du filtre < 500ms après changement de critère |

### 5.2 Persistance

| Élément | Persistance |
|---------|-------------|
| Filtres sélectionnés | Session utilisateur (localStorage) |
| Filtres favoris | Base de données (profil utilisateur) |
| Filtre par défaut | Configurable par admin |

### 5.3 États spéciaux

| État | Comportement |
|------|--------------|
| Aucun véhicule trouvé | Message "Aucun véhicule ne correspond aux filtres" + suggestion de relâcher un filtre |
| Tous les filtres vides | Afficher tous les véhicules actifs |
| Filtre invalide | Ignorer silencieusement + indicateur visuel sur le filtre problématique |
| Chargement | Skeleton loader sur la liste + spinner discret sur la carte |

---

## 6. Modèle de données — Filtre

```csharp
public class FiltreCarte
{
    public Guid Id { get; set; }
    public Guid UtilisateurId { get; set; }
    public string Nom { get; set; } // Nom du filtre sauvegardé
    public bool EstFavori { get; set; }
    
    // Filtres véhicule
    public List<string> VehiculeIds { get; set; } = new();
    public List<StatutVehicule> Statuts { get; set; } = new() { StatutVehicule.Actif };
    public List<TypeVehicule> Types { get; set; } = new();
    public List<Guid> GroupeIds { get; set; } = new();
    public List<Guid> ConducteurIds { get; set; } = new();
    public decimal? VitesseMin { get; set; }
    public decimal? VitesseMax { get; set; }
    public bool? EnMouvement { get; set; }
    public TimeSpan? DerniereActiviteDepuis { get; set; }
    
    // Filtres zone
    public List<Guid> ZoneIds { get; set; } = new();
    public PositionRelativeZone PositionRelative { get; set; } = PositionRelativeZone.Tous;
    public PointGps? CentreRayon { get; set; }
    public decimal? RayonKm { get; set; }
    
    // Filtres alerte
    public bool? AlerteActive { get; set; }
    public List<NiveauSeverite> Severites { get; set; } = new();
    public List<TypeAlerte> TypesAlerte { get; set; } = new();
    
    // Filtre temporel
    public PeriodeAffichage Periode { get; set; } = PeriodeAffichage.TempsReel;
    public bool AfficherHistoriqueTrajet { get; set; } = false;
    
    // Métadonnées
    public DateTime DateCreation { get; set; }
    public DateTime DateModification { get; set; }
}

public enum StatutVehicule { Actif, Inactif, EnMaintenance, HorsService }
public enum TypeVehicule { Camion, Voiture, Moto, Utilitaire }
public enum PositionRelativeZone { DansLaZone, HorsZone, Tous }
public enum NiveauSeverite { Avertissement, Alerte, Critique }
public enum TypeAlerte { Vitesse, Geofencing, Inactivite }
public enum PeriodeAffichage { TempsReel, DerniereHeure, Aujourdhui, Personnalise }

public class PointGps
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
```

---

## 7. API Endpoints prévus

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| GET | `/api/vehicules?filtre={json}` | Récupérer véhicules filtrés |
| GET | `/api/filtres` | Lister les filtres sauvegardés de l'utilisateur |
| POST | `/api/filtres` | Sauvegarder un nouveau filtre |
| PUT | `/api/filtres/{id}` | Modifier un filtre existant |
| DELETE | `/api/filtres/{id}` | Supprimer un filtre |
| GET | `/api/filtres/{id}/resultats` | Exécuter un filtre sauvegardé |

---

## 8. Contraintes et limites

| Contrainte | Valeur | Justification |
|------------|--------|---------------|
| Max véhicules affichés simultanément | 500 | Performance carte (markers) |
| Max filtres sauvegardés par utilisateur | 20 | UX simple |
| Max critères combinés | 10 actifs | Requête performante |
| Timeout requête filtre | 3 secondes | Expérience utilisateur |
| Rafraîchissement auto | 5 secondes | Balance serveur/temps réel |

---

## 9. Intégrations

| Module | Intégration | Description |
|--------|-------------|-------------|
| GEO-9 (Zones géo) | `ZoneGeographique` | Filtrer par zones définies |
| GEO-10 (Alertes) | `AlerteVitesse` | Filtrer par alerte active/sévérité |
| GEO-48 (Modèle) | `Appareil`, `HistoriqueEvenement` | Données véhicule et position |
| SignalR Hub | Temps réel | Mise à jour live des positions filtrées |

---

## 10. Critères de validation (Definition of Done)

- [ ] Tous les critères de filtre sont documentés avec type et valeurs
- [ ] La logique de combinaison (AND/OR) est définie
- [ ] Le modèle de données FiltreCarte est spécifié
- [ ] Les endpoints API sont listés
- [ ] Les contraintes de performance sont établies
- [ ] Les comportements temps réel sont documentés
- [ ] L'intégration avec GEO-9 et GEO-10 est prévue
