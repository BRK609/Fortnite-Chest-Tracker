# Fortnite Chest Tracker

**Fortnite Chest Tracker** est une application Windows permettant de suivre les coffres ouverts et détruits dans Fortnite grâce à l'analyse des fichiers replay.

## Fonctionnalités

- Analyse automatique des replays Fortnite.
- Comptabilisation des coffres ouverts et détruits.
- Historique des parties analysées.
- Statistiques personnelles.
- Interface graphique Windows.
- Sélection du joueur à analyser à partir des données du replay.
- Sauvegarde locale des statistiques.

## Installation

1. Ouvrir la section **Releases** de ce dépôt GitHub.
2. Télécharger `FortniteChestTracker.exe`.
3. Lancer l'application.

Aucune installation manuelle de .NET n'est normalement nécessaire avec la version autonome distribuée.

## Configuration de Fortnite

Pour que l'application puisse analyser les parties :

1. Ouvrir les paramètres de Fortnite.
2. Aller dans la section **Jeu**.
3. Activer l'option de sauvegarde des replays.
4. Jouer une partie.

Les parties jouées avant l'activation de cette option ne peuvent pas être récupérées sous forme de replay.

## Données et confidentialité

Les statistiques et le profil du joueur sont enregistrés localement sur l'ordinateur de l'utilisateur.

L'application utilise le dossier :

`%LOCALAPPDATA%\FortniteChestTracker`

Les données personnelles et les fichiers replay ne sont pas inclus dans le dépôt public.

## Informations techniques

- Langage : C#
- Framework : .NET 10
- Plateforme : Windows 10/11
- Architecture : x64

## Crédits

Ce projet utilise notamment **Fortnite-ChestCounter** et ses bibliothèques associées.

Les licences et crédits des projets utilisés sont conservés dans leurs dossiers respectifs.

## Avertissement

Fortnite Chest Tracker est un projet indépendant et n'est ni affilié, ni associé, ni approuvé par Epic Games.

Fortnite est une marque déposée d'Epic Games, Inc.
