Ligne dans le code en C# pour exposer en local sur un reseau wifi : 
`_listener.Prefixes.Add($"http://*:{Port}/");`

Ouvrir Powershell en mode amdin :

````
netsh http add urlacl url=http://*:8080/ sddl=D:(A;;GX;;;WD)
netsh advfirewall firewall add rule name="LobbyServer" dir=in action=allow protocol=TCP localport=8080
````

Trouver l'IPv4 via la commande `ipconfig` sous le menu du wifi.

Lancer le server.

Coté Client, il permettre au client d'acceder aux pages de controlleur :
Pour ca on va sur le server lancer un petit server http :
````powershell
python -m http.server 8000
````

Toujour en gardant l'IP de l'ordi sur le reseau en tete, on va depuis le client chercher :
````html
http://<IPv4>/<page.html>
````