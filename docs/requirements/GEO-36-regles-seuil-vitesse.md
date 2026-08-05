# GEO-36 : Seuil de vitesse configurable et règles de déclenchement

## 1. Vue d'ensemble

Ce document définit les règles métier pour le système d'alerte de vitesse de GeoTrack.
Le système surveille la vitesse des appareils GPS en temps réel et déclenche des alertes
lorsque les seuils configurés sont dépassés.

---

## 2. Paramètres de configuration du seuil

### 2.1 Structure de configuration

| Paramètre | Type | Unité | Plage valide | Valeur par défaut |
|-----------|------|-------|--------------|-------------------|
| `SeuilVitesseMin` | decimal | km/h | 5 – 300 | 50 |
| `SeuilVitesseMax` | decimal | km/h | 10 – 400 | 120 |
| `TolerancePourcentage` | int | % | 0 – 30 | 10 |
| `DureeMinDepassement` | int | secondes | 3 – 120 | 5 |
| `IntervalleVerification` | int | secondes | 1 – 60 | 3 |
| `NombreEchantillonsConfirmation` | int | — | 1 – 10 | 2 |

### 2.2 Niveaux de seuil

Le système supporte 3 niveaux de seuil configurables par zone :

| Niveau | Nom | Calcul | Sévérité |
|--------|-----|--------|----------|
| 1 | **Avertissement** | Seuil × (1 + Tolérance%) | Faible |
| 2 | **Alerte** | Seuil × 1.0 (dépassement confirmé) | Moyenne |
| 3 | **Critique** | Seuil × 1.5 (excès dangereux) | Élevée |

**Exemple concret :**
- Seuil configuré = 50 km/h, Tolérance = 10%
- Niveau 1 (Avertissement) : > 55 km/h (50 × 1.10)
- Niveau 2 (Alerte) : > 50 km/h pendant ≥ 5 secondes consécutives
- Niveau 3 (Critique) : > 75 km/h (50 × 1.50)

---

## 3. Règles de déclenchement

### 3.1 Algorithme de détection

```
POUR CHAQUE échantillon GPS reçu (intervalle = IntervalleVerification) :
    1. Calculer la vitesse instantanée (distance / temps entre 2 points GPS)
    2. Appliquer le filtre de bruit (moyenne mobile sur 3 échantillons)
    3. Comparer la vitesse filtrée au seuil de la zone actuelle

    SI vitesse > seuil ALORS :
        - Incrémenter le compteur de dépassement
        - SI compteur >= NombreEchantillonsConfirmation ALORS :
            - Calculer la durée de dépassement continu
            - SI durée >= DureeMinDepassement ALORS :
                → DÉCLENCHER l'alerte (niveau approprié)
                → Enregistrer l'événement dans HistoriqueEvenements
            FIN SI
        FIN SI
    SINON :
        - Réinitialiser le compteur de dépassement
    FIN SI
FIN POUR
```

### 3.2 Conditions de déclenchement (résumé)

| Condition | Requis | Justification |
|-----------|--------|---------------|
| Vitesse > Seuil | ✅ Oui | Condition primaire |
| Échantillons consécutifs ≥ N | ✅ Oui | Éviter les faux positifs GPS |
| Durée ≥ DureeMinDepassement | ✅ Oui | Confirmer le dépassement réel |
| Appareil actif | ✅ Oui | Ignorer les appareils éteints |
| Zone avec alerte vitesse activée | ✅ Oui | Respecter la config par zone |

### 3.3 Conditions de NON-déclenchement

| Situation | Comportement |
|-----------|-------------|
| GPS en tunnel (perte signal) | Ignorer — pas d'alerte |
| Vitesse = 0 puis saut brusque | Appliquer le filtre de bruit |
| Appareil hors zone surveillée | Aucune vérification |
| Alerte déjà envoyée (anti-spam) | Cooldown actif — pas de doublon |

---

## 4. Règles anti-spam

### 4.1 Mécanisme de cooldown

| Paramètre | Valeur | Description |
|-----------|--------|-------------|
| `CooldownMinutes` | 5 | Temps minimum entre 2 alertes identiques |
| `MaxAlertesParHeure` | 10 | Maximum d'alertes par appareil par heure |
| `MaxAlertesParJour` | 50 | Maximum d'alertes par appareil par jour |
| `EscaladeApresN` | 3 | Escalader la sévérité après N alertes consécutives |

### 4.2 Logique anti-spam

```
AVANT d'envoyer une alerte :
    1. Vérifier si une alerte identique a été envoyée dans les CooldownMinutes dernières
       → Si OUI : supprimer l'alerte (log uniquement)
    2. Compter les alertes de l'heure pour cet appareil
       → Si >= MaxAlertesParHeure : bloquer + notifier l'administrateur
    3. Compter les alertes du jour pour cet appareil
       → Si >= MaxAlertesParJour : désactiver les alertes + notifier admin
    4. Compter les alertes consécutives non acquittées
       → Si >= EscaladeApresN : escalader la sévérité au niveau supérieur
```

---

## 5. Canaux de notification

### 5.1 Matrice de notification par sévérité

| Sévérité | Push App | Email | SMS | Dashboard | Son |
|----------|----------|-------|-----|-----------|-----|
| Faible (Avertissement) | ✅ | ❌ | ❌ | ✅ | Bip simple |
| Moyenne (Alerte) | ✅ | ✅ | ❌ | ✅ | Bip double |
| Élevée (Critique) | ✅ | ✅ | ✅ | ✅ | Alarme continue |

### 5.2 Contenu de la notification

```json
{
  "type": "ALERTE_VITESSE",
  "severite": "MOYENNE",
  "appareil": {
    "id": "uuid",
    "nom": "Véhicule 01"
  },
  "details": {
    "vitesseDetectee": 72.5,
    "seuilConfigure": 50.0,
    "exces": 22.5,
    "unite": "km/h",
    "dureeDepassement": "00:00:12",
    "zone": "Campus UQO - Alexandre-Taché"
  },
  "position": {
    "latitude": 45.4765,
    "longitude": -75.7013
  },
  "horodatage": "2026-08-05T12:30:00Z"
}
```

---

## 6. Cas d'utilisation

### CU-01 : Dépassement simple confirmé
1. Appareil GPS envoie position toutes les 3 secondes
2. Vitesse calculée = 65 km/h (seuil zone = 50 km/h)
3. 2 échantillons consécutifs confirment le dépassement
4. Durée > 5 secondes → **Alerte niveau 2 (Moyenne) déclenchée**
5. Notification push + email envoyés

### CU-02 : Pic de vitesse transitoire (faux positif évité)
1. Appareil GPS envoie position
2. Vitesse calculée = 80 km/h (erreur GPS ponctuelle)
3. Échantillon suivant = 48 km/h (retour normal)
4. Compteur réinitialisé → **Aucune alerte**

### CU-03 : Excès critique
1. Vitesse calculée = 120 km/h (seuil = 50 km/h, soit > 150%)
2. Déclenchement immédiat niveau 3 (pas de délai pour critique)
3. Notification push + email + SMS + alarme sonore

### CU-04 : Anti-spam en action
1. Appareil génère 11e alerte dans l'heure
2. MaxAlertesParHeure = 10 dépassé
3. Alerte bloquée, événement logué
4. Notification administrateur : "Appareil X génère un volume anormal d'alertes"

---

## 7. Personnalisation par zone

Chaque zone peut avoir ses propres paramètres :

```csharp
public class ConfigurationAlerteVitesse
{
    public Guid ZoneId { get; set; }
    public decimal SeuilVitesseKmH { get; set; } = 50m;
    public int TolerancePourcentage { get; set; } = 10;
    public int DureeMinDepassementSec { get; set; } = 5;
    public int NombreEchantillonsConfirmation { get; set; } = 2;
    public int CooldownMinutes { get; set; } = 5;
    public int MaxAlertesParHeure { get; set; } = 10;
    public bool AlerteVitesseActivee { get; set; } = true;
    public NiveauSeverite SeuilCritiquePourcentage { get; set; } = 150;
}
```

---

## 8. Validation et contraintes

| Règle de validation | Condition | Message d'erreur |
|--------------------|-----------|-----------------|
| RV-01 | SeuilVitesse > 0 | "Le seuil de vitesse doit être positif" |
| RV-02 | SeuilVitesse ≤ 400 | "Le seuil ne peut excéder 400 km/h" |
| RV-03 | Tolérance ∈ [0, 30] | "La tolérance doit être entre 0% et 30%" |
| RV-04 | DureeMin ≥ 3 | "La durée minimum est de 3 secondes" |
| RV-05 | Cooldown ≥ 1 | "Le cooldown minimum est de 1 minute" |
| RV-06 | Zone existe et est active | "La zone spécifiée n'existe pas ou est inactive" |

---

## 9. Diagramme d'état de l'alerte

```
┌─────────────┐    vitesse > seuil     ┌──────────────────┐
│   NORMAL    │ ──────────────────────► │  EN_OBSERVATION  │
│  (aucune    │                         │  (compteur < N)  │
│   alerte)   │ ◄────────────────────── │                  │
└─────────────┘    vitesse ≤ seuil      └──────────────────┘
                                               │
                                               │ compteur ≥ N
                                               │ ET durée ≥ min
                                               ▼
┌─────────────┐    cooldown expiré      ┌──────────────────┐
│  COOLDOWN   │ ──────────────────────► │    DÉCLENCHÉE    │
│  (attente)  │ ◄────────────────────── │  (alerte émise)  │
└─────────────┘    alerte envoyée       └──────────────────┘
                                               │
                                               │ N alertes consécutives
                                               ▼
                                        ┌──────────────────┐
                                        │    ESCALADÉE     │
                                        │ (sévérité ↑)     │
                                        └──────────────────┘
```

---

## 10. Intégration avec le modèle de données (GEO-48)

Ce document s'appuie sur l'entité `RegleAlerte` définie dans GEO-48 :

| Champ du modèle | Utilisation ici |
|-----------------|-----------------|
| `SeuilVitesseKmH` | Seuil configurable principal |
| `TypeEvenement` | DEPASSEMENT_VITESSE |
| `Severite` | Faible / Moyenne / Élevée |
| `CanauxNotification` | JSON des canaux actifs |
| `CooldownMinutes` | Mécanisme anti-spam |
| `EstActif` | Activation/désactivation par zone |

---

## 11. Critères d'acceptation

- [x] CA-01 : Le seuil de vitesse est configurable par zone (5-400 km/h)
- [x] CA-02 : La tolérance est configurable (0-30%)
- [x] CA-03 : Le nombre d'échantillons de confirmation est configurable (1-10)
- [x] CA-04 : La durée minimum de dépassement est configurable (3-120s)
- [x] CA-05 : 3 niveaux de sévérité sont définis (Avertissement, Alerte, Critique)
- [x] CA-06 : Le mécanisme anti-spam empêche les alertes en doublon
- [x] CA-07 : L'escalade automatique est documentée
- [x] CA-08 : Les canaux de notification sont définis par sévérité
- [x] CA-09 : Les cas de faux positifs GPS sont gérés (filtre de bruit)
- [x] CA-10 : La validation des paramètres est spécifiée

---

*Document : GEO-36 | Auteur : Sory Fofana | Date : 2026-08-05*
*Story parente : GEO-10 (Alerte de vitesse) | Sprint : GEN1423 Groupe 8*
