# GEO-16 — Spécification Architecture Haute Disponibilité
**Story** : En tant que système, je souhaite continuer à fonctionner en cas de panne d'un composant afin d'assurer une haute disponibilité 24/7.
**Epic** : GEO-4 — Gestion de la flotte et fiabilité
**Story points** : 8 | **Due date** : 2026-08-10

---

## 1. Critères d'acceptation

| # | Critère | Validation |
|---|---------|------------|
| 1 | Une panne partielle n'interrompt pas tout le système | Tests de résilience (Circuit Breaker, Retry) |
| 2 | Un mécanisme de secours est documenté | Documentation failover + runbook |

---

## 2. Architecture haute disponibilité

### 2.1 Composants critiques GeoTrack

| Composant | Rôle | Risque panne |
|-----------|------|-------------|
| API REST | Endpoints véhicules, alertes, trajets | Critique |
| Service GPS | Réception positions temps réel | Critique |
| Base de données | Stockage trajets, véhicules, alertes | Critique |
| Service alertes (GEO-9) | Détection dépassements | Élevé |
| Service notifications (GEO-10) | Envoi alertes | Moyen |
| Service filtre carte (GEO-7) | Visualisation carte | Moyen |

### 2.2 Patterns de résilience

#### Circuit Breaker
- **États** : Fermé (normal) → Ouvert (panne) → Semi-ouvert (test)
- **Seuil ouverture** : 5 échecs consécutifs en 30s
- **Durée ouverture** : 60s avant test semi-ouvert
- **Applicable à** : API externe GPS, service notifications, base de données

#### Retry avec Backoff exponentiel
- **Tentatives** : 3 max
- **Délais** : 1s → 2s → 4s
- **Applicable à** : Appels base de données, service alertes

#### Fallback / Mode dégradé
- **GPS indisponible** : Dernière position connue affichée (max 5 min)
- **DB indisponible** : Cache mémoire 60s pour lectures
- **Notifications indisponibles** : File d'attente locale, retry différé
- **Filtre carte indisponible** : Affichage tous véhicules sans filtre

#### Health Checks
- **Endpoint** : `GET /api/health` — statut global
- **Endpoint** : `GET /api/health/details` — statut par composant
- **Fréquence** : Toutes les 30s
- **Timeout** : 5s par composant

---

## 3. Modèle de données résilience

```csharp
public enum StatutComposant { Operationnel, Degrade, Indisponible }
public enum StatutCircuitBreaker { Ferme, Ouvert, SemiOuvert }

public class HealthStatus
{
    public string Composant { get; set; }
    public StatutComposant Statut { get; set; }
    public DateTime DerniereVerification { get; set; }
    public string MessageErreur { get; set; }
    public int NbEchecsConsecutifs { get; set; }
    public TimeSpan TempsReponse { get; set; }
}

public class CircuitBreakerConfig
{
    public int SeuilEchecs { get; set; } = 5;
    public TimeSpan DureeOuverture { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan FenetreTemps { get; set; } = TimeSpan.FromSeconds(30);
}
```

---

## 4. Endpoints API résilience

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| GET | `/api/health` | Statut global système |
| GET | `/api/health/details` | Statut détaillé par composant |
| GET | `/api/health/circuit-breakers` | État tous les circuit breakers |
| POST | `/api/health/circuit-breakers/{nom}/reset` | Reset manuel circuit breaker |
| GET | `/api/health/metrics` | Métriques disponibilité (uptime, taux erreur) |

---

## 5. Mécanisme de secours (Runbook)

### Scénario 1 : Panne API GPS
1. Circuit Breaker s'ouvre après 5 échecs
2. Dernière position connue affichée sur carte (badge "Dernière position connue")
3. Alerte dashboard : composant GPS dégradé
4. Retry automatique toutes les 60s
5. Notification admin si panne > 5 min

### Scénario 2 : Panne base de données
1. Cache mémoire activé (lectures 60s)
2. Écritures mises en file d'attente locale
3. Circuit Breaker ouvert
4. Dashboard affiche statut "Mode dégradé"
5. Synchronisation automatique au retour DB

### Scénario 3 : Panne service notifications
1. Alertes mises en file d'attente locale
2. Retry avec backoff exponentiel (1s → 2s → 4s)
3. Max 3 tentatives puis log erreur
4. Notifications envoyées au retour du service

---

## 6. Contraintes techniques

| Contrainte | Valeur |
|------------|--------|
| Disponibilité cible | 99.5% (cours GEN1423) |
| Temps réponse health check | < 500ms |
| Durée max mode dégradé GPS | 5 minutes |
| Taille cache mémoire | Max 1000 entrées |
| Rétention logs résilience | 30 jours |

---

## 7. Intégrations

| Story | Intégration |
|-------|-------------|
| GEO-7 | Filtre carte → fallback affichage tous véhicules |
| GEO-9 | Alertes → file d'attente si service indisponible |
| GEO-10 | Notifications → retry différé |
| GEO-15 | CRUD véhicule → cache lecture si DB dégradée |
| GEO-12 | Historique → cache 60s si DB indisponible |
