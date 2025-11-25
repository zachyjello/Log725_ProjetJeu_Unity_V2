# LOG725_ProjetOmbres_Unity

## Instructions rapides pour le multijoueur

- Pour que le multijoueur fonctionne correctement, **lancez le jeu depuis la scène `NetworkSetup` dans Unity**.
- Ouvrez la scène `NetworkSetup` avant de cliquer sur Play.
- Cela initialise Mirror et la gestion des joueurs pour le lobby et la partie.

## TODO
 Présentement, il y a quelques trucs à compléter pour la remise finale qui sont assez importants

 Priorité 1
 - Fixer le problèmes de git lfs et créer un nouveau repos clean
 - Fixer les problèmes d'ombres en diminuant les textures de la deuxième map et en modifiant les paramètres des lumières pour limiter la création de shadow maps
 - Fixer les problèmes de spawn liés au networking pour avoir une meilleure fiabilité avec le multijoueur
 - Fixer le spawn des clés
 
 Priorité 2
 - Améliorer la luminosité de base, puisque tout est complètement noir sans éclairage
 - Permettre la sélection des rôles dans le lobby
 - Améliorer la caméra (beaucoup de clipping et de trucs étranges)
 - Ajouter des player models
 - Ajouter un indicateur pour que les ombres puissent plonger ou non