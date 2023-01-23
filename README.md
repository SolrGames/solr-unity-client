# solr-unity-client
Hi there! This is a Unity Client for the Solr Scorekeeper Solana program. The program is currently deployed on devnet only. The base client was generated using the [Garbles Client Generator](https://github.com/garbles-labs/Solana.Unity.Anchor).


This SDK should be considered pre-release and will very likely change in the future. That said, you can use it now! Let us know what you think in any of these places:


[Twitter](https://twitter.com/solr_games)

[Discord](https://discord.gg/NwfUPA4d)

[Github Issues](https://github.com/SolrGames/solr-unity-client/issues)


## Basic Usage

To start using the client, you must first create a verifier by calling the InitVerifier instruction.

Once you have a verifier, you can create GameSeasons for your game. A GameSeason can have as many or as few
players as you like. 

To submit scores, you must register a player for a GameSeason. This is only done once per player. The player id used here is for you and should correspond to something meaningful in your game, e.g. a GUID or PublicKey string. 

After a player is registered, you can submit scores using your Verifier. 

### Example Usage with Scorekeeper
For example usage of the Scorekeeper client, see Solr.Example.cs

An example flow using the functions from Solr.Example.cs

```C#
UnityEngine.Debug.Log("Creating account...");

var example = new Scorekeeper.Example.Example();
ScorekeeperClient client = new ScorekeeperClient(rpcClient, streamingRpcClient);

string playerId = "45a0f81f-3d0c-4e17-9ccf-0710864f8326";
ushort seasonId = 1;
ushort scoreUpdate = 100;

// Only do this once for your game
UnityEngine.Debug.Log("Creating verifier account...");
var verifierAccount = await example.InitVerifierAccount();
UnityEngine.Debug.Log($"Verifier Result: {verifierAccount.Verifier.Key}");

// Only do this once per game season
UnityEngine.Debug.Log("Creating game season account...");
var createdGameSeason = await example.InitNewGameSeason();
UnityEngine.Debug.Log($"GameSeason  Result: {createdGameSeason.PlayerCount}");

// This is a public key for your game. The one here is a test PublicKey based on the keypair used
// in the Example code
var verifierAccountAddress = Scorekeeper.Accounts.VerifierAccount.FindProgramAddress(new PublicKey("CboGra4fhDm14GbcdkfLpzC9uaVcwpMFykJ1vzNXwwVB"));
UnityEngine.Debug.Log($"Using verifier: {verifierAccountAddress.Key}");

var gameSeasonAccount = Scorekeeper.Accounts.GameSeason.FindProgramAddress(verifierAccountAddress, seasonId);
var gameSeason = await client.GetGameSeasonAsync(gameSeasonAccount);
UnityEngine.Debug.Log($"Using season: {gameSeason.ParsedResult.Count} key {gameSeasonAccount}");

// Only do this once per new player in the season
UnityEngine.Debug.Log("Registering player for season...");
var player = await example.RegisterPlayerForSeason(Convert.ToUInt16(0), playerId);
UnityEngine.Debug.Log($"Player Result: {player.PlayerId} : {player.Score}");

// used for accumulating scores
UnityEngine.Debug.Log("Incremeneting player score...");
var playerIncremented = await example.IncrementPlayerScore(Convert.ToUInt16(0), playerId, scoreUpdate);
UnityEngine.Debug.Log($"Player Result: {playerIncremented.PlayerId} : {playerIncremented.Score}");

// used for accumulating scores
UnityEngine.Debug.Log("Update player score...");
var playerUpdated = await example.UpdatePlayerScore(Convert.ToUInt16(0), playerId, scoreUpdate);
UnityEngine.Debug.Log($"Player Result: {playerUpdated.PlayerId} : {playerUpdated.Score}");
```