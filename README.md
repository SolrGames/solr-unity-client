# solr-unity-client

## Basic Usage

To start using the client, you must first create a verifier by calling the InitVerifier instruction.

Once you have a verifier, you can create GameSeasons for your game. A GameSeason can have as many or as few
players as you like. 

To submit scores, you must register a player for a GameSeason. This is only done once per player. The player id used here is for you and should correspond to something meaningful in your game, e.g. a GUID or PublicKey string. 

After a player is registered, you can submit scores using your Verifier. 
