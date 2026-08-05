-- ============================================================================
-- GEO-49 : Script de migration - Base de données GeoTrack
-- Projet : GeoTrack (GEN1423 - Groupe 8)
-- Story parente : GEO-9 (Zone Géographique)
-- Auteur : Sory Fofana
-- Date : 2026-08-05
-- Description : Création des tables pour le module de geofencing
-- ============================================================================

-- ============================================================================
-- SECTION 1 : CRÉATION DE LA BASE DE DONNÉES
-- ============================================================================

-- Utiliser la base de données GeoTrack (créer si nécessaire)
-- Pour SQL Server :
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'GeoTrackDB')
BEGIN
    CREATE DATABASE GeoTrackDB;
END
GO

USE GeoTrackDB;
GO

-- ============================================================================
-- SECTION 2 : TABLE DES UTILISATEURS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Utilisateurs')
BEGIN
    CREATE TABLE Utilisateurs (
        -- Clé primaire
        Id                  INT IDENTITY(1,1) PRIMARY KEY,

        -- Informations personnelles
        Nom                 NVARCHAR(100)   NOT NULL,
        Prenom              NVARCHAR(100)   NOT NULL,
        Email               NVARCHAR(255)   NOT NULL,
        Telephone           NVARCHAR(20)    NULL,

        -- Authentification
        MotDePasseHash      NVARCHAR(512)   NOT NULL,
        Sel                 NVARCHAR(128)   NOT NULL,

        -- Rôle et permissions
        Role                NVARCHAR(50)    NOT NULL DEFAULT 'Utilisateur',
        -- Valeurs possibles : 'Administrateur', 'Gestionnaire', 'Utilisateur', 'Lecteur'

        -- Préférences de notification
        NotificationEmail   BIT             NOT NULL DEFAULT 1,
        NotificationSms     BIT             NOT NULL DEFAULT 0,
        NotificationPush    BIT             NOT NULL DEFAULT 1,

        -- Métadonnées
        DateCreation        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        DateModification    DATETIME2       NULL,
        DerniereConnexion   DATETIME2       NULL,
        EstActif            BIT             NOT NULL DEFAULT 1,
        EstSupprime         BIT             NOT NULL DEFAULT 0,
        DateSuppression     DATETIME2       NULL,

        -- Contraintes
        CONSTRAINT UQ_Utilisateurs_Email UNIQUE (Email),
        CONSTRAINT CK_Utilisateurs_Role CHECK (Role IN ('Administrateur', 'Gestionnaire', 'Utilisateur', 'Lecteur'))
    );

    PRINT 'Table Utilisateurs créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 3 : TABLE DES APPAREILS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appareils')
BEGIN
    CREATE TABLE Appareils (
        -- Clé primaire
        Id                      INT IDENTITY(1,1) PRIMARY KEY,

        -- Identification de l'appareil
        Nom                     NVARCHAR(100)   NOT NULL,
        IdentifiantUnique       NVARCHAR(255)   NOT NULL,
        TypeAppareil            NVARCHAR(50)    NOT NULL,
        -- Valeurs possibles : 'Smartphone', 'TraceurGPS', 'Vehicule', 'Objet'

        -- Dernière position connue
        DerniereLatitude        DECIMAL(10, 7)  NULL,
        DerniereLongitude       DECIMAL(10, 7)  NULL,
        DerniereVitesse         DECIMAL(8, 2)   NULL,  -- en km/h
        DernierAzimut           DECIMAL(5, 2)   NULL,  -- en degrés (0-360)
        DernierePositionDate    DATETIME2       NULL,

        -- Configuration
        IntervalleRafraichissement INT          NOT NULL DEFAULT 30, -- en secondes
        EstEnLigne              BIT             NOT NULL DEFAULT 0,

        -- Relation avec l'utilisateur propriétaire
        UtilisateurId           INT             NOT NULL,

        -- Métadonnées
        DateCreation            DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        DateModification        DATETIME2       NULL,
        EstActif                BIT             NOT NULL DEFAULT 1,
        EstSupprime             BIT             NOT NULL DEFAULT 0,
        DateSuppression         DATETIME2       NULL,

        -- Contraintes
        CONSTRAINT UQ_Appareils_IdentifiantUnique UNIQUE (IdentifiantUnique),
        CONSTRAINT FK_Appareils_Utilisateurs FOREIGN KEY (UtilisateurId)
            REFERENCES Utilisateurs(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Appareils_TypeAppareil CHECK (TypeAppareil IN ('Smartphone', 'TraceurGPS', 'Vehicule', 'Objet')),
        CONSTRAINT CK_Appareils_Latitude CHECK (DerniereLatitude IS NULL OR (DerniereLatitude >= -90 AND DerniereLatitude <= 90)),
        CONSTRAINT CK_Appareils_Longitude CHECK (DerniereLongitude IS NULL OR (DerniereLongitude >= -180 AND DerniereLongitude <= 180)),
        CONSTRAINT CK_Appareils_Vitesse CHECK (DerniereVitesse IS NULL OR DerniereVitesse >= 0)
    );

    PRINT 'Table Appareils créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 4 : TABLE DES ZONES GÉOGRAPHIQUES
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ZonesGeographiques')
BEGIN
    CREATE TABLE ZonesGeographiques (
        -- Clé primaire
        Id                      INT IDENTITY(1,1) PRIMARY KEY,

        -- Identification de la zone
        Nom                     NVARCHAR(150)   NOT NULL,
        Description             NVARCHAR(500)   NULL,

        -- Type de zone
        TypeZone                NVARCHAR(20)    NOT NULL,
        -- Valeurs possibles : 'Inclusion', 'Exclusion'

        -- Géométrie
        FormeGeometrique        NVARCHAR(20)    NOT NULL,
        -- Valeurs possibles : 'Cercle', 'Polygone', 'Rectangle'

        -- Coordonnées (format JSON pour flexibilité)
        -- Cercle : {"centre": {"lat": 45.48, "lng": -75.78}, "rayon": 500}
        -- Polygone : {"points": [{"lat": 45.48, "lng": -75.78}, ...]}
        -- Rectangle : {"nordEst": {"lat": 45.49, "lng": -75.77}, "sudOuest": {"lat": 45.47, "lng": -75.79}}
        CoordonneesJson         NVARCHAR(MAX)   NOT NULL,

        -- Rayon (uniquement pour les cercles, en mètres)
        Rayon                   DECIMAL(10, 2)  NULL,

        -- Centre de la zone (pour optimisation spatiale)
        CentreLatitude          DECIMAL(10, 7)  NOT NULL,
        CentreLongitude         DECIMAL(10, 7)  NOT NULL,

        -- Couleur d'affichage sur la carte
        CouleurHex              NVARCHAR(7)     NOT NULL DEFAULT '#3388ff',

        -- Propriétaire
        UtilisateurId           INT             NOT NULL,

        -- Métadonnées
        DateCreation            DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        DateModification        DATETIME2       NULL,
        EstActive               BIT             NOT NULL DEFAULT 1,
        EstSupprimee            BIT             NOT NULL DEFAULT 0,
        DateSuppression         DATETIME2       NULL,

        -- Contraintes
        CONSTRAINT FK_Zones_Utilisateurs FOREIGN KEY (UtilisateurId)
            REFERENCES Utilisateurs(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Zones_TypeZone CHECK (TypeZone IN ('Inclusion', 'Exclusion')),
        CONSTRAINT CK_Zones_FormeGeometrique CHECK (FormeGeometrique IN ('Cercle', 'Polygone', 'Rectangle')),
        CONSTRAINT CK_Zones_CentreLatitude CHECK (CentreLatitude >= -90 AND CentreLatitude <= 90),
        CONSTRAINT CK_Zones_CentreLongitude CHECK (CentreLongitude >= -180 AND CentreLongitude <= 180),
        CONSTRAINT CK_Zones_Rayon CHECK (Rayon IS NULL OR (Rayon > 0 AND Rayon <= 100000))
    );

    PRINT 'Table ZonesGeographiques créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 5 : TABLE DES RÈGLES D'ALERTE
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReglesAlerte')
BEGIN
    CREATE TABLE ReglesAlerte (
        -- Clé primaire
        Id                      INT IDENTITY(1,1) PRIMARY KEY,

        -- Identification
        Nom                     NVARCHAR(150)   NOT NULL,
        Description             NVARCHAR(500)   NULL,

        -- Zone associée
        ZoneGeographiqueId      INT             NOT NULL,

        -- Type d'événement déclencheur
        TypeEvenement           NVARCHAR(30)    NOT NULL,
        -- Valeurs : 'Entree', 'Sortie', 'Vitesse', 'Immobilite', 'EntreeEtSortie'

        -- Seuils
        SeuilVitesseKmh         DECIMAL(8, 2)   NULL,  -- pour alertes de vitesse
        SeuilImmobiliteMinutes  INT             NULL,  -- pour alertes d'immobilité

        -- Sévérité
        Severite                NVARCHAR(20)    NOT NULL DEFAULT 'Moyenne',
        -- Valeurs : 'Critique', 'Haute', 'Moyenne', 'Basse', 'Information'

        -- Canaux de notification (JSON)
        -- Ex : ["Email", "SMS", "Push", "Webhook"]
        CanauxNotificationJson  NVARCHAR(500)   NOT NULL DEFAULT '["Push"]',

        -- Anti-spam
        DelaiMinimalMinutes     INT             NOT NULL DEFAULT 5,
        NombreMaxParHeure       INT             NOT NULL DEFAULT 10,

        -- Plages horaires d'activation (JSON, null = toujours actif)
        -- Ex : {"lundi": {"debut": "08:00", "fin": "18:00"}, ...}
        PlagesHorairesJson      NVARCHAR(MAX)   NULL,

        -- Propriétaire
        UtilisateurId           INT             NOT NULL,

        -- Métadonnées
        DateCreation            DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        DateModification        DATETIME2       NULL,
        EstActive               BIT             NOT NULL DEFAULT 1,
        EstSupprimee            BIT             NOT NULL DEFAULT 0,
        DateSuppression         DATETIME2       NULL,

        -- Contraintes
        CONSTRAINT FK_Regles_Zones FOREIGN KEY (ZoneGeographiqueId)
            REFERENCES ZonesGeographiques(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Regles_Utilisateurs FOREIGN KEY (UtilisateurId)
            REFERENCES Utilisateurs(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Regles_TypeEvenement CHECK (TypeEvenement IN ('Entree', 'Sortie', 'Vitesse', 'Immobilite', 'EntreeEtSortie')),
        CONSTRAINT CK_Regles_Severite CHECK (Severite IN ('Critique', 'Haute', 'Moyenne', 'Basse', 'Information')),
        CONSTRAINT CK_Regles_SeuilVitesse CHECK (SeuilVitesseKmh IS NULL OR (SeuilVitesseKmh > 0 AND SeuilVitesseKmh <= 300)),
        CONSTRAINT CK_Regles_DelaiMinimal CHECK (DelaiMinimalMinutes >= 1 AND DelaiMinimalMinutes <= 1440),
        CONSTRAINT CK_Regles_NombreMax CHECK (NombreMaxParHeure >= 1 AND NombreMaxParHeure <= 100)
    );

    PRINT 'Table ReglesAlerte créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 6 : TABLE DE L'HISTORIQUE DES ÉVÉNEMENTS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriqueEvenements')
BEGIN
    CREATE TABLE HistoriqueEvenements (
        -- Clé primaire
        Id                      BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Événement
        TypeEvenement           NVARCHAR(30)    NOT NULL,
        -- Valeurs : 'Entree', 'Sortie', 'DepassementVitesse', 'Immobilite'

        -- Position au moment de l'événement
        Latitude                DECIMAL(10, 7)  NOT NULL,
        Longitude               DECIMAL(10, 7)  NOT NULL,
        VitesseKmh              DECIMAL(8, 2)   NULL,
        Azimut                  DECIMAL(5, 2)   NULL,
        Precision               DECIMAL(6, 2)   NULL,  -- précision GPS en mètres

        -- Relations
        AppareilId              INT             NOT NULL,
        ZoneGeographiqueId      INT             NOT NULL,
        RegleAlerteId           INT             NULL,

        -- Notification
        AlerteEnvoyee           BIT             NOT NULL DEFAULT 0,
        DateAlerteEnvoyee       DATETIME2       NULL,
        CanalUtilise            NVARCHAR(50)    NULL,

        -- Contexte supplémentaire (JSON)
        -- Ex : {"distanceLimite": 15.3, "tempsImmobilite": 600}
        ContexteJson            NVARCHAR(MAX)   NULL,

        -- Métadonnées
        DateEvenement           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        DateCreation            DATETIME2       NOT NULL DEFAULT GETUTCDATE(),

        -- Contraintes
        CONSTRAINT FK_Historique_Appareils FOREIGN KEY (AppareilId)
            REFERENCES Appareils(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Historique_Zones FOREIGN KEY (ZoneGeographiqueId)
            REFERENCES ZonesGeographiques(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Historique_Regles FOREIGN KEY (RegleAlerteId)
            REFERENCES ReglesAlerte(Id) ON DELETE SET NULL,
        CONSTRAINT CK_Historique_TypeEvenement CHECK (TypeEvenement IN ('Entree', 'Sortie', 'DepassementVitesse', 'Immobilite')),
        CONSTRAINT CK_Historique_Latitude CHECK (Latitude >= -90 AND Latitude <= 90),
        CONSTRAINT CK_Historique_Longitude CHECK (Longitude >= -180 AND Longitude <= 180)
    );

    PRINT 'Table HistoriqueEvenements créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 7 : TABLE D'ASSOCIATION ZONES-APPAREILS
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ZonesAppareils')
BEGIN
    CREATE TABLE ZonesAppareils (
        -- Clé primaire composite
        ZoneGeographiqueId      INT NOT NULL,
        AppareilId              INT NOT NULL,

        -- Statut actuel de l'appareil dans la zone
        EstDansLaZone           BIT NOT NULL DEFAULT 0,
        DateDerniereDetection   DATETIME2 NULL,

        -- Métadonnées
        DateAssociation         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Clé primaire composite
        CONSTRAINT PK_ZonesAppareils PRIMARY KEY (ZoneGeographiqueId, AppareilId),

        -- Clés étrangères
        CONSTRAINT FK_ZA_Zones FOREIGN KEY (ZoneGeographiqueId)
            REFERENCES ZonesGeographiques(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ZA_Appareils FOREIGN KEY (AppareilId)
            REFERENCES Appareils(Id) ON DELETE CASCADE
    );

    PRINT 'Table ZonesAppareils créée avec succès.';
END
GO

-- ============================================================================
-- SECTION 8 : INDEX DE PERFORMANCE
-- ============================================================================

-- Index pour recherche rapide par utilisateur
CREATE NONCLUSTERED INDEX IX_Appareils_UtilisateurId
    ON Appareils(UtilisateurId)
    WHERE EstSupprime = 0;

CREATE NONCLUSTERED INDEX IX_Zones_UtilisateurId
    ON ZonesGeographiques(UtilisateurId)
    WHERE EstSupprimee = 0;

CREATE NONCLUSTERED INDEX IX_Regles_UtilisateurId
    ON ReglesAlerte(UtilisateurId)
    WHERE EstSupprimee = 0;

-- Index pour recherche par zone
CREATE NONCLUSTERED INDEX IX_Regles_ZoneId
    ON ReglesAlerte(ZoneGeographiqueId)
    WHERE EstSupprimee = 0;

-- Index pour l'historique (requêtes fréquentes)
CREATE NONCLUSTERED INDEX IX_Historique_AppareilId_Date
    ON HistoriqueEvenements(AppareilId, DateEvenement DESC);

CREATE NONCLUSTERED INDEX IX_Historique_ZoneId_Date
    ON HistoriqueEvenements(ZoneGeographiqueId, DateEvenement DESC);

CREATE NONCLUSTERED INDEX IX_Historique_TypeEvenement_Date
    ON HistoriqueEvenements(TypeEvenement, DateEvenement DESC);

-- Index spatial (approximation avec centre + rayon pour optimisation)
CREATE NONCLUSTERED INDEX IX_Zones_Centre
    ON ZonesGeographiques(CentreLatitude, CentreLongitude)
    WHERE EstSupprimee = 0 AND EstActive = 1;

-- Index pour les appareils en ligne
CREATE NONCLUSTERED INDEX IX_Appareils_EnLigne
    ON Appareils(EstEnLigne)
    INCLUDE (DerniereLatitude, DerniereLongitude, DerniereVitesse)
    WHERE EstSupprime = 0 AND EstActif = 1;

PRINT 'Index de performance créés avec succès.';
GO

-- ============================================================================
-- SECTION 9 : DONNÉES DE TEST (SEED)
-- ============================================================================

-- Utilisateur de test
INSERT INTO Utilisateurs (Nom, Prenom, Email, MotDePasseHash, Sel, Role)
VALUES (
    'Fofana', 'Sory', 'fofs01@uqo.ca',
    'HASH_PLACEHOLDER_A_REMPLACER',
    'SEL_PLACEHOLDER_A_REMPLACER',
    'Administrateur'
);

INSERT INTO Utilisateurs (Nom, Prenom, Email, MotDePasseHash, Sel, Role)
VALUES (
    'Hien', 'Florian', 'hief01@uqo.ca',
    'HASH_PLACEHOLDER_A_REMPLACER',
    'SEL_PLACEHOLDER_A_REMPLACER',
    'Gestionnaire'
);

-- Appareil de test
INSERT INTO Appareils (Nom, IdentifiantUnique, TypeAppareil, UtilisateurId, IntervalleRafraichissement)
VALUES (
    'Téléphone Sory', 'APP-001-SMARTPHONE', 'Smartphone', 1, 15
);

-- Zone de test (Université du Québec en Outaouais - Campus principal)
INSERT INTO ZonesGeographiques (Nom, Description, TypeZone, FormeGeometrique, CoordonneesJson, Rayon, CentreLatitude, CentreLongitude, CouleurHex, UtilisateurId)
VALUES (
    'Campus UQO - Alexandre-Taché',
    'Zone d''inclusion couvrant le campus principal de l''UQO',
    'Inclusion',
    'Cercle',
    '{"centre": {"lat": 45.4287, "lng": -75.7377}, "rayon": 300}',
    300.00,
    45.4287000,
    -75.7377000,
    '#2196F3',
    1
);

-- Zone de test (Zone interdite)
INSERT INTO ZonesGeographiques (Nom, Description, TypeZone, FormeGeometrique, CoordonneesJson, CentreLatitude, CentreLongitude, CouleurHex, UtilisateurId)
VALUES (
    'Zone de construction - Rue Laurier',
    'Zone d''exclusion temporaire - travaux routiers',
    'Exclusion',
    'Polygone',
    '{"points": [{"lat": 45.4295, "lng": -75.7390}, {"lat": 45.4298, "lng": -75.7370}, {"lat": 45.4285, "lng": -75.7368}, {"lat": 45.4282, "lng": -75.7388}]}',
    45.4290000,
    -75.7379000,
    '#F44336',
    1
);

-- Règle d'alerte de test
INSERT INTO ReglesAlerte (Nom, Description, ZoneGeographiqueId, TypeEvenement, Severite, CanauxNotificationJson, DelaiMinimalMinutes, NombreMaxParHeure, UtilisateurId)
VALUES (
    'Alerte sortie campus',
    'Notification quand un appareil quitte le périmètre du campus UQO',
    1,
    'Sortie',
    'Moyenne',
    '["Push", "Email"]',
    10,
    5,
    1
);

INSERT INTO ReglesAlerte (Nom, Description, ZoneGeographiqueId, TypeEvenement, SeuilVitesseKmh, Severite, CanauxNotificationJson, DelaiMinimalMinutes, NombreMaxParHeure, UtilisateurId)
VALUES (
    'Alerte vitesse zone construction',
    'Alerte si vitesse excessive près de la zone de construction',
    2,
    'Vitesse',
    30.00,
    'Haute',
    '["Push", "SMS"]',
    5,
    8,
    1
);

PRINT 'Données de test insérées avec succès.';
GO

-- ============================================================================
-- SECTION 10 : VUES UTILES
-- ============================================================================

-- Vue : Zones actives avec nombre de règles
CREATE OR ALTER VIEW vw_ZonesActives AS
SELECT
    z.Id,
    z.Nom,
    z.TypeZone,
    z.FormeGeometrique,
    z.CentreLatitude,
    z.CentreLongitude,
    z.Rayon,
    z.CouleurHex,
    z.DateCreation,
    u.Prenom + ' ' + u.Nom AS Proprietaire,
    (SELECT COUNT(*) FROM ReglesAlerte r WHERE r.ZoneGeographiqueId = z.Id AND r.EstSupprimee = 0) AS NombreRegles,
    (SELECT COUNT(*) FROM ZonesAppareils za WHERE za.ZoneGeographiqueId = z.Id AND za.EstDansLaZone = 1) AS AppareilsDansZone
FROM ZonesGeographiques z
INNER JOIN Utilisateurs u ON z.UtilisateurId = u.Id
WHERE z.EstSupprimee = 0 AND z.EstActive = 1;
GO

-- Vue : Derniers événements (24h)
CREATE OR ALTER VIEW vw_DerniersEvenements AS
SELECT
    h.Id,
    h.TypeEvenement,
    h.Latitude,
    h.Longitude,
    h.VitesseKmh,
    h.DateEvenement,
    h.AlerteEnvoyee,
    a.Nom AS NomAppareil,
    z.Nom AS NomZone,
    z.TypeZone
FROM HistoriqueEvenements h
INNER JOIN Appareils a ON h.AppareilId = a.Id
INNER JOIN ZonesGeographiques z ON h.ZoneGeographiqueId = z.Id
WHERE h.DateEvenement >= DATEADD(HOUR, -24, GETUTCDATE());
GO

PRINT '=== Migration GEO-49 terminée avec succès ===';
PRINT 'Tables créées : Utilisateurs, Appareils, ZonesGeographiques, ReglesAlerte, HistoriqueEvenements, ZonesAppareils';
PRINT 'Index créés : 9 index de performance';
PRINT 'Vues créées : vw_ZonesActives, vw_DerniersEvenements';
PRINT 'Données seed : 2 utilisateurs, 1 appareil, 2 zones, 2 règles';
GO
