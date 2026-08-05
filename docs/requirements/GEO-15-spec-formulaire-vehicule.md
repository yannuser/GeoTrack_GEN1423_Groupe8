# GEO-15 — Spécification : Formulaire d'ajout de véhicule

## 1. Contexte

**Story** : En tant qu'administrateur, je souhaite ajouter facilement un nouveau véhicule à la flotte afin de suivre son évolution.

**Critères d'acceptation** :
1. Un formulaire d'ajout de véhicule est disponible
2. Le nouveau véhicule apparaît sur la carte dès la réception de sa première position GPS

**Epic parent** : GEO-3 — Historique et tableau de bord analytique

---

## 2. Champs du formulaire

| # | Champ | Type | Obligatoire | Validation | Exemple |
|---|-------|------|-------------|------------|---------|
| 1 | Identifiant véhicule | Texte (auto-généré) | Oui | Format VEH-XXXX | VEH-0042 |
| 2 | Immatriculation | Texte | Oui | Format plaque QC : ABC 1234 | FLT 2025 |
| 3 | Marque | Liste déroulante | Oui | Valeurs prédéfinies | Ford |
| 4 | Modèle | Texte | Oui | 2-50 caractères | Transit Connect |
| 5 | Année | Nombre | Oui | 2000 ≤ année ≤ année+1 | 2024 |
| 6 | Type de véhicule | Liste déroulante | Oui | Camion/Fourgon/Voiture/Utilitaire | Fourgon |
| 7 | Couleur | Texte | Non | 2-30 caractères | Blanc |
| 8 | Numéro VIN | Texte | Non | 17 caractères alphanumériques | 1FTBW2CM5JKA12345 |
| 9 | Kilométrage initial | Nombre | Non | ≥ 0 | 45000 |
| 10 | Date mise en service | Date | Oui | ≤ aujourd'hui | 2024-06-15 |
| 11 | Groupe/Équipe | Liste déroulante | Non | Groupes existants | Équipe Nord |
| 12 | Conducteur assigné | Recherche | Non | Conducteurs existants | Jean Dupont |
| 13 | ID tracker GPS | Texte | Oui | Format TRK-XXXXXXXX | TRK-A1B2C3D4 |
| 14 | Notes | Zone de texte | Non | Max 500 caractères | Véhicule neuf |

---

## 3. Règles métier

### 3.1 Unicité
- L'immatriculation doit être unique dans la flotte
- Le numéro VIN (si fourni) doit être unique
- L'ID tracker GPS doit être unique et non déjà assigné

### 3.2 Apparition sur la carte
- Dès la soumission du formulaire, le véhicule est créé en base avec le statut **« En attente GPS »**
- Le marqueur apparaît sur la carte **uniquement** à la réception de la première position GPS
- Icône spéciale « nouveau véhicule » pendant les premières 24h
- Notification push à l'administrateur : « Véhicule VEH-XXXX localisé pour la première fois »

### 3.3 Statuts du véhicule après création
| Statut | Condition | Icône carte |
|--------|-----------|-------------|
| En attente GPS | Créé, aucune position reçue | ❌ Non affiché |
| Actif - Arrêté | Position reçue, vitesse = 0 | 🔵 Bleu |
| Actif - En mouvement | Position reçue, vitesse > 0 | 🟢 Vert |
| Hors ligne | Pas de signal > 10 min | ⚫ Gris |

### 3.4 Validation temps réel
- Validation côté client (JavaScript) à la saisie
- Validation côté serveur (C# Data Annotations) à la soumission
- Messages d'erreur en français, affichés sous le champ concerné

---

## 4. API Endpoints

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| POST | `/api/vehicules` | Créer un nouveau véhicule |
| GET | `/api/vehicules/immatriculation/{plaque}/exists` | Vérifier unicité plaque |
| GET | `/api/vehicules/tracker/{trackerId}/exists` | Vérifier unicité tracker |
| GET | `/api/vehicules/groupes` | Liste des groupes disponibles |
| GET | `/api/vehicules/conducteurs/disponibles` | Conducteurs non assignés |
| GET | `/api/vehicules/{id}/premiere-position` | Statut première localisation |

---

## 5. Modèle de données

```csharp
public class Vehicule
{
    public int Id { get; set; }
    public string Identifiant { get; set; }          // VEH-XXXX
    public string Immatriculation { get; set; }      // ABC 1234
    public string Marque { get; set; }
    public string Modele { get; set; }
    public int Annee { get; set; }
    public TypeVehicule Type { get; set; }
    public string? Couleur { get; set; }
    public string? NumeroVIN { get; set; }
    public int? KilometrageInitial { get; set; }
    public DateTime DateMiseEnService { get; set; }
    public int? GroupeId { get; set; }
    public int? ConducteurId { get; set; }
    public string TrackerGpsId { get; set; }         // TRK-XXXXXXXX
    public string? Notes { get; set; }
    public StatutVehicule Statut { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DatePremierePosition { get; set; }
}

public enum TypeVehicule
{
    Camion,
    Fourgon,
    Voiture,
    Utilitaire
}

public enum StatutVehicule
{
    EnAttenteGPS,
    ActifArrete,
    ActifEnMouvement,
    HorsLigne
}
```

---

## 6. Flux utilisateur

```
┌─────────────────────────────────────────────────────────┐
│  Administrateur clique "Ajouter un véhicule"            │
└─────────────────────┬───────────────────────────────────┘
                      ▼
┌─────────────────────────────────────────────────────────┐
│  Formulaire s'affiche (14 champs)                       │
│  - Validation temps réel côté client                    │
│  - Auto-complétion conducteur/groupe                    │
└─────────────────────┬───────────────────────────────────┘
                      ▼
┌─────────────────────────────────────────────────────────┐
│  Soumission → POST /api/vehicules                       │
│  - Validation serveur                                   │
│  - Vérification unicité (plaque, VIN, tracker)          │
└─────────────┬───────────────────────┬───────────────────┘
              ▼                       ▼
┌─────────────────────┐   ┌─────────────────────────────┐
│  ❌ Erreur          │   │  ✅ Succès                   │
│  Afficher message   │   │  Véhicule créé (En attente) │
│  sous le champ      │   │  Redirection vers fiche     │
└─────────────────────┘   └──────────────┬──────────────┘
                                         ▼
                          ┌─────────────────────────────┐
                          │  Première position GPS reçue │
                          │  → Marqueur sur la carte    │
                          │  → Notification admin       │
                          └─────────────────────────────┘
```

---

## 7. Contraintes techniques

| Contrainte | Valeur |
|------------|--------|
| Temps de réponse POST | < 500ms |
| Taille max notes | 500 caractères |
| Format tracker | TRK- + 8 alphanum |
| Délai apparition carte | < 5s après première position |
| Notification première position | Push + entrée journal |

---

## 8. Intégrations

- **GEO-7** : Le véhicule ajouté est filtrable par les critères définis
- **GEO-10** : Les alertes vitesse s'appliquent dès apparition sur la carte
- **GEO-9** : Le véhicule peut être assigné à une zone géographique

---

## 9. Critères de validation (Definition of Done)

- [ ] Formulaire fonctionnel avec les 14 champs
- [ ] Validations client + serveur
- [ ] Unicité vérifiée (plaque, VIN, tracker)
- [ ] Véhicule apparaît sur carte à la première position GPS
- [ ] Notification envoyée à l'admin
- [ ] Tests unitaires couvrent le CRUD + validations
