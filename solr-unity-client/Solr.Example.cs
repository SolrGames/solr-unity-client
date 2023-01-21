using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scorekeeper.Program;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Scorekeeper
{
    namespace Example
    {
        public class Example 
        {
            private const string MnemonicWords = "that barrel write fix differ room bag shrimp base suffer space behave";
            private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.DevNet);
            private static readonly IStreamingRpcClient streamingRpcClient = ClientFactory.GetStreamingClient(Cluster.DevNet);
            private static ScorekeeperClient client = new ScorekeeperClient(rpcClient, streamingRpcClient);

            private static Wallet GameWallet
            {
                get
                {
                    Wallet gameWallet = new Wallet(MnemonicWords);
                    return gameWallet;
                }
            }

            private static async Task<RequestResult<string>> SimulateAndSendTransaction(byte[] transaction)
            {
                try
                {
                    // simulate for error checking
                    RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(transaction);
                    UnityEngine.Debug.Log($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + txSim.Result.Value.Logs);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log($"Simulation Exception: {e}");
                }

                // send and confirm tx
                RequestResult<string> txSignature = await rpcClient.SendTransactionAsync(transaction);
                UnityEngine.Debug.Log($"Tx Signature: {txSignature.Result}");

                // wait for confirmation. Despite specifying finalized commitment status, sometimes it isn't done yet
                await rpcClient.GetConfirmedTransactionAsync(txSignature.Result);

                return txSignature;
            }

            // This initializes a verifier account. You only need to do this once.
            public async Task<Accounts.VerifierAccount> InitVerifierAccount()
            {
                Account verifierWalletAccount = GameWallet.GetAccount(0);
                UnityEngine.Debug.Log($"Game wallet pubkey: {verifierWalletAccount.PublicKey}");

                InitVerifierAccountAccounts accounts = new InitVerifierAccountAccounts();
                accounts.Verifier = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                accounts.VerifierPubKey = verifierWalletAccount.PublicKey;
                UnityEngine.Debug.Log("Instruction accounts created");

                // build transaction to send
                RequestResult<ResponseValue<BlockHash>> blockhashResult = await rpcClient.GetRecentBlockHashAsync();
                byte[] tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(verifierWalletAccount.PublicKey)
                    .AddInstruction(ScorekeeperProgram.InitVerifierAccount(accounts))
                    .Build(verifierWalletAccount);

                // note this will fail if the verifier account already exists
                UnityEngine.Debug.Log("Transaction built");
                await SimulateAndSendTransaction(tx);
                UnityEngine.Debug.Log("Transaction sent and confirmed");

                var result = await client.GetVerifierAccountAsync(accounts.Verifier);
                return result.WasSuccessful ? result.ParsedResult : null;
            }

            // inits a new game season from an initialized verifier account. This is only
            // needed once per season. Player scores are contained within the game season (you can have only one if you like)
            //
            // Note this example will create a new season each time it is called. You should keep track of your season count
            // and only call this when you want to start a new season.
            public async Task<Accounts.GameSeason> InitGameSeason()
            {
                Account verifierWalletAccount = GameWallet.GetAccount(0);
                UnityEngine.Debug.Log($"Game wallet pubkey: {verifierWalletAccount.PublicKey}");

                PublicKey verifierAccountKey = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                Accounts.VerifierAccount verifierData = (await client.GetVerifierAccountAsync(verifierAccountKey)).ParsedResult;

                InitGameSeasonAccounts accounts = new InitGameSeasonAccounts();
                accounts.Verifier = verifierWalletAccount;
                accounts.VerifierAccount = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                accounts.GameSeason = Accounts.GameSeason.FindProgramAddress(accounts.Verifier, Convert.ToUInt16(verifierData.SeasonCount));
                UnityEngine.Debug.Log("Instruction accounts created");

                // build transaction to send
                RequestResult<ResponseValue<BlockHash>> blockhashResult = await rpcClient.GetRecentBlockHashAsync();
                byte[] tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(verifierWalletAccount.PublicKey)
                    .AddInstruction(ScorekeeperProgram.InitGameSeason(accounts))
                    .Build(verifierWalletAccount);
                UnityEngine.Debug.Log("Transaction built");
                await SimulateAndSendTransaction(tx);
                UnityEngine.Debug.Log("Transaction sent and confirmed");

                UnityEngine.Debug.Log($"GameSeasonAccountKey {accounts.GameSeason}");
                var result = await client.GetGameSeasonAsync(accounts.GameSeason);
                UnityEngine.Debug.Log($"GameSeasonAccount result {result.WasSuccessful}");
                UnityEngine.Debug.Log($"GameSeasonAccount init status result {result.ParsedResult.IsInitialized}");
                return result.WasSuccessful ? result.ParsedResult : null;
            }

            public async Task<Accounts.Player> RegisterPlayerForSeason(ushort seasonNumber, string playerId)
            {
                Account verifierWalletAccount = GameWallet.GetAccount(0);
                UnityEngine.Debug.Log($"Game wallet pubkey: {verifierWalletAccount.PublicKey}");
                
                PublicKey verifierAccountKey = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                Accounts.VerifierAccount verifierData = (await client.GetVerifierAccountAsync(verifierAccountKey)).ParsedResult;
                
                // Note that player IDs are a string. They should be unique for each player in your game. For example a UUID
                // or Base58 encoded public key. This is used to generate the player account address.
                

                InitPlayerAccountForSeasonAccounts accounts = new InitPlayerAccountForSeasonAccounts();
                accounts.Verifier = verifierWalletAccount;
                accounts.VerifierAccount = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                accounts.Player = Accounts.Player.FindProgramAddress(verifierWalletAccount.PublicKey, Convert.ToUInt16(0), playerId);
                accounts.GameSeason = Accounts.GameSeason.FindProgramAddress(accounts.Verifier, seasonNumber);
                UnityEngine.Debug.Log("Instruction accounts created");

                // build transaction to send
                RequestResult<ResponseValue<BlockHash>> blockhashResult = await rpcClient.GetRecentBlockHashAsync();
                byte[] tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(verifierWalletAccount.PublicKey)
                    .AddInstruction(ScorekeeperProgram.InitPlayerAccountForSeason(accounts, seasonNumber, playerId))
                    .Build(verifierWalletAccount);
                UnityEngine.Debug.Log("Transaction built");
                await SimulateAndSendTransaction(tx);
                UnityEngine.Debug.Log("Transaction sent and confirmed");

                UnityEngine.Debug.Log($"Player account key {accounts.Player}");
                var result = await client.GetPlayerAsync(accounts.Player);
                UnityEngine.Debug.Log($"GameSeasonAccount result {result.WasSuccessful}");
                UnityEngine.Debug.Log($"GameSeasonAccount init status result {result.ParsedResult.IsInitialized}");
                return result.WasSuccessful ? result.ParsedResult : null;
            }

            public async Task<Accounts.Player> UpdatePlayerScore(ushort seasonNumber, string playerId, ulong score)
            {
                Account verifierWalletAccount = GameWallet.GetAccount(0);
                UnityEngine.Debug.Log($"Game wallet pubkey: {verifierWalletAccount.PublicKey}");
                
                PublicKey verifierAccountKey = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                Accounts.VerifierAccount verifierData = (await client.GetVerifierAccountAsync(verifierAccountKey)).ParsedResult;
                
                // Note that player IDs are a string. They should be unique for each player in your game. For example a UUID
                // or Base58 encoded public key. This is used to generate the player account address.
                
                IncrementPlayerScoreAccounts accounts = new IncrementPlayerScoreAccounts();
                accounts.Verifier = verifierWalletAccount;
                accounts.VerifierAccount = Accounts.VerifierAccount.FindProgramAddress(verifierWalletAccount.PublicKey);
                accounts.Player = Accounts.Player.FindProgramAddress(verifierWalletAccount.PublicKey, Convert.ToUInt16(0), playerId);
                accounts.GameSeason = Accounts.GameSeason.FindProgramAddress(accounts.Verifier, seasonNumber);
                UnityEngine.Debug.Log("Instruction accounts created");

                // build transaction to send
                RequestResult<ResponseValue<BlockHash>> blockhashResult = await rpcClient.GetRecentBlockHashAsync();
                byte[] tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(verifierWalletAccount.PublicKey)
                    .AddInstruction(ScorekeeperProgram.IncrementPlayerScore(accounts, seasonNumber, playerId, score))
                    .Build(verifierWalletAccount);
                UnityEngine.Debug.Log("Transaction built");
                await SimulateAndSendTransaction(tx);
                UnityEngine.Debug.Log("Transaction sent and confirmed");

                UnityEngine.Debug.Log($"Player account key {accounts.Player}");
                var result = await client.GetPlayerAsync(accounts.Player);
                UnityEngine.Debug.Log($"GameSeasonAccount result {result.WasSuccessful}");
                UnityEngine.Debug.Log($"GameSeasonAccount init status result {result.ParsedResult.IsInitialized}");
                return result.WasSuccessful ? result.ParsedResult : null;
            }

        }
    }
}
