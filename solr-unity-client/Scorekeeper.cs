using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Scorekeeper;
using Scorekeeper.Program;
using Scorekeeper.Errors;
using Scorekeeper.Accounts;
using System.Text;

namespace Scorekeeper
{
    namespace Utilities
    {
        /// <summary>
        /// Data holder type for a program-derived address with bump. 
        /// </summary>
        public class ProgramDerivedAddress
        {
            /// <summary>
            /// Gets the public key. 
            /// </summary>
            public PublicKey PublicKey { get; private set; }

            /// <summary>
            /// Gets the byte bump offset.
            /// </summary>
            public byte Bump { get; private set; }

            public ProgramDerivedAddress(PublicKey publicKey, byte bump = 255)
            {
                this.PublicKey = publicKey;
                this.Bump = bump;
            }

            public static implicit operator PublicKey(ProgramDerivedAddress pda) => (pda != null) ? pda.PublicKey : null;

            public static ProgramDerivedAddress FindProgramAddress(IEnumerable<byte[]> seeds, PublicKey programId)
            {
                byte nonce = 255;

                while (nonce != 0)
                {
                    UnityEngine.Debug.Log($"Nonce: {nonce}");
                    PublicKey address = null;
                    List<byte[]> seedsWithNonce = new List<byte[]>(seeds);
                    seedsWithNonce.Add(new byte[] { nonce });

                    //try to generate the address 
                    bool created = false;
                    try
                    {
                        created = PublicKey.TryCreateProgramAddress(new List<byte[]>(seedsWithNonce), programId, out address);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.Log($"Exception: {e}");
                    }

                    //if succeeded, return 
                    if (created)
                    {
                        UnityEngine.Debug.Log($"Created pda address: {created}");
                        return new ProgramDerivedAddress(address, nonce);
                    }

                    //decrease the nonce and retry if failed 
                    nonce--;
                }

                return null;
            }
        }
    }

    namespace Accounts
    {
        public partial class GameSeason
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 10697364714611120222UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[] { 94, 152, 49, 231, 135, 172, 116, 148 };
            public static string ACCOUNT_DISCRIMINATOR_B58 => "GpgkCXTfZ3H";
            public PublicKey Verifier { get; set; }

            public ushort Count { get; set; }

            public uint PlayerCount { get; set; }

            public bool IsFinalized { get; set; }

            public bool IsInitialized { get; set; }

            public static Scorekeeper.Utilities.ProgramDerivedAddress FindProgramAddress(PublicKey verifier, ushort seasonNumber)
            {
                return Scorekeeper.Utilities.ProgramDerivedAddress.FindProgramAddress(new[]
                {
                    BitConverter.GetBytes(seasonNumber),
                    verifier.KeyBytes,
                },
                ScorekeeperClient.ProgramId);
            }

            public static GameSeason Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                GameSeason result = new GameSeason();
                result.Verifier = _data.GetPubKey(offset);
                offset += 32;
                result.Count = _data.GetU16(offset);
                offset += 2;
                result.PlayerCount = _data.GetU32(offset);
                offset += 4;
                result.IsFinalized = _data.GetBool(offset);
                offset += 1;
                result.IsInitialized = _data.GetBool(offset);
                offset += 1;
                return result;
            }
        }

        public partial class VerifierAccount
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 11329559588664539217UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[] { 81, 120, 248, 87, 107, 174, 58, 157 };
            public static string ACCOUNT_DISCRIMINATOR_B58 => "EdPNTW3Bcq2";
            public PublicKey Verifier { get; set; }

            public ushort SeasonCount { get; set; }

            public bool IsInitialized { get; set; }

            public static Scorekeeper.Utilities.ProgramDerivedAddress FindProgramAddress(PublicKey verifier)
            {
                return Scorekeeper.Utilities.ProgramDerivedAddress.FindProgramAddress(new[]
                {
                    Encoding.UTF8.GetBytes("verifier"),
                    verifier.KeyBytes,
                },
                ScorekeeperClient.ProgramId);
            }

            public static VerifierAccount Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                VerifierAccount result = new VerifierAccount();
                result.Verifier = _data.GetPubKey(offset);
                offset += 32;
                result.SeasonCount = _data.GetU16(offset);
                offset += 2;
                result.IsInitialized = _data.GetBool(offset);
                offset += 1;
                return result;
            }
        }

        public partial class Player
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 15766710478567431885UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[] { 205, 222, 112, 7, 165, 155, 206, 218 };
            public static string ACCOUNT_DISCRIMINATOR_B58 => "bSBoKNsSHuj";
            public PublicKey Verifier { get; set; }

            public string PlayerId { get; set; }

            public PublicKey GameSeason { get; set; }

            public ulong Score { get; set; }

            public uint Count { get; set; }

            public bool IsInitialized { get; set; }

            public static Scorekeeper.Utilities.ProgramDerivedAddress FindProgramAddress(PublicKey verifier, ushort seasonNumber, string playerId)
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append(seasonNumber);
                    builder.Append(playerId);
                    builder.Append(verifier.Key);
                    byte[] seeds = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));

                    return Scorekeeper.Utilities.ProgramDerivedAddress.FindProgramAddress(new[]
                        {
                            seeds
                        },
                        ScorekeeperClient.ProgramId);
                }
            }

            public static Player Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Player result = new Player();
                result.Verifier = _data.GetPubKey(offset);
                offset += 32;
                offset += _data.GetBorshString(offset, out var resultPlayerId);
                result.PlayerId = resultPlayerId;
                result.GameSeason = _data.GetPubKey(offset);
                offset += 32;
                result.Score = _data.GetU64(offset);
                offset += 8;
                result.Count = _data.GetU32(offset);
                offset += 4;
                result.IsInitialized = _data.GetBool(offset);
                offset += 1;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum ScorekeeperErrorKind : uint
        {
            GameSeasonAlreadyInitialized = 6000U,
            GameSeasonNotInitialized = 6001U,
            PlayerAlreadyInitialized = 6002U,
            PlayerNotInitialized = 6003U,
            InvalidPlayerId = 6004U,
            ExeedsMaxScore = 6005U,
            GameSeasonEnded = 6006U,
            VerifierAlreadyInitialized = 6007U,
            InvalidSeasonCount = 6008U
        }
    }

    public partial class ScorekeeperClient : TransactionalBaseClient<ScorekeeperErrorKind>
    {
        public static PublicKey ProgramId = new PublicKey("BiKAQA1j8vCUEj9UxAUq4BNb82bZQQQihTcxPStxhjcm");

        public ScorekeeperClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient) : base(rpcClient, streamingRpcClient, ProgramId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameSeason>>> GetGameSeasonsAsync(Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp> { new Solana.Unity.Rpc.Models.MemCmp { Bytes = GameSeason.ACCOUNT_DISCRIMINATOR_B58, Offset = 0 } };
            var res = await RpcClient.GetProgramAccountsAsync(ProgramId, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameSeason>>(res);
            List<GameSeason> resultingAccounts = new List<GameSeason>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => GameSeason.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameSeason>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<VerifierAccount>>> GetVerifierAccountsAsync(Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp> { new Solana.Unity.Rpc.Models.MemCmp { Bytes = VerifierAccount.ACCOUNT_DISCRIMINATOR_B58, Offset = 0 } };
            var res = await RpcClient.GetProgramAccountsAsync(ProgramId, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<VerifierAccount>>(res);
            List<VerifierAccount> resultingAccounts = new List<VerifierAccount>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => VerifierAccount.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<VerifierAccount>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Player>>> GetPlayersAsync(Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp> { new Solana.Unity.Rpc.Models.MemCmp { Bytes = Player.ACCOUNT_DISCRIMINATOR_B58, Offset = 0 } };
            var res = await RpcClient.GetProgramAccountsAsync(ProgramId, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Player>>(res);
            List<Player> resultingAccounts = new List<Player>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Player.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Player>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<GameSeason>> GetGameSeasonAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<GameSeason>(res);
            var resultingAccount = GameSeason.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<GameSeason>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<VerifierAccount>> GetVerifierAccountAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<VerifierAccount>(res);
            var resultingAccount = VerifierAccount.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<VerifierAccount>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Player>> GetPlayerAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Player>(res);
            var resultingAccount = Player.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Player>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeGameSeasonAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, GameSeason> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                GameSeason parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = GameSeason.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribeVerifierAccountAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, VerifierAccount> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                VerifierAccount parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = VerifierAccount.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribePlayerAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Player> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Player parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Player.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitVerifierAccountAsync(InitVerifierAccountAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.ScorekeeperProgram.InitVerifierAccount(accounts);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendInitGameSeasonAsync(InitGameSeasonAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.ScorekeeperProgram.InitGameSeason(accounts);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendFinalizeGameSeasonAsync(FinalizeGameSeasonAccounts accounts, ushort seasonCount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.ScorekeeperProgram.FinalizeGameSeason(accounts, seasonCount);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendInitPlayerAccountForSeasonAsync(InitPlayerAccountForSeasonAccounts accounts, ushort seasonCount, string playerId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.ScorekeeperProgram.InitPlayerAccountForSeason(accounts, seasonCount, playerId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendIncrementPlayerScoreAsync(IncrementPlayerScoreAccounts accounts, ushort seasonCount, string playerId, ulong increment, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.ScorekeeperProgram.IncrementPlayerScore(accounts, seasonCount, playerId, increment);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<ScorekeeperErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<ScorekeeperErrorKind>> { { 6000U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.GameSeasonAlreadyInitialized, "Game season already initialized") }, { 6001U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.GameSeasonNotInitialized, "Game season not initialized") }, { 6002U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.PlayerAlreadyInitialized, "Player already initialized") }, { 6003U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.PlayerNotInitialized, "Player not initialized") }, { 6004U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.InvalidPlayerId, "Player ID must match player account") }, { 6005U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.ExeedsMaxScore, "Score exceeds max score") }, { 6006U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.GameSeasonEnded, "Game season has ended") }, { 6007U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.VerifierAlreadyInitialized, "Verifier already initialized") }, { 6008U, new ProgramError<ScorekeeperErrorKind>(ScorekeeperErrorKind.InvalidSeasonCount, "Season count value greater than existing season count") }, };
        }
    }

    namespace Program
    {
        public class InitVerifierAccountAccounts
        {
            public PublicKey Verifier { get; set; }

            public PublicKey VerifierPubKey { get; set; }

            public PublicKey SystemProgram
            {
                get { return Solana.Unity.Programs.SystemProgram.ProgramIdKey; }
            }
        }

        public class InitGameSeasonAccounts
        {
            public PublicKey Verifier { get; set; }

            public PublicKey GameSeason { get; set; }

            public PublicKey VerifierAccount { get; set; }

            public PublicKey SystemProgram
            {
                get { return Solana.Unity.Programs.SystemProgram.ProgramIdKey; }
            }
        }

        public class FinalizeGameSeasonAccounts
        {
            public PublicKey Verifier { get; set; }

            public PublicKey GameSeason { get; set; }

            public PublicKey VerifierAccount { get; set; }
        }

        public class InitPlayerAccountForSeasonAccounts
        {
            public PublicKey Verifier { get; set; }

            public PublicKey VerifierAccount { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey GameSeason { get; set; }

            public PublicKey SystemProgram
            {
                get { return Solana.Unity.Programs.SystemProgram.ProgramIdKey; }
            }
        }

        public class IncrementPlayerScoreAccounts
        {
            public PublicKey Verifier { get; set; }

            public PublicKey VerifierAccount { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey GameSeason { get; set; }

            public PublicKey SystemProgram
            {
                get { return Solana.Unity.Programs.SystemProgram.ProgramIdKey; }
            }
        }

        public static class ScorekeeperProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitVerifierAccount(InitVerifierAccountAccounts accounts)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Verifier, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.VerifierPubKey, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9855283811452052773UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction { Keys = keys, ProgramId = ScorekeeperClient.ProgramId.KeyBytes, Data = resultData };
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction InitGameSeason(InitGameSeasonAccounts accounts)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Verifier, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameSeason, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.VerifierAccount, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(318020572081959874UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction { Keys = keys, ProgramId = ScorekeeperClient.ProgramId.KeyBytes, Data = resultData };
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction FinalizeGameSeason(FinalizeGameSeasonAccounts accounts, ushort seasonCount)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Verifier, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameSeason, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.VerifierAccount, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(8750348940270879912UL, offset);
                offset += 8;
                _data.WriteU16(seasonCount, offset);
                offset += 2;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction { Keys = keys, ProgramId = ScorekeeperClient.ProgramId.KeyBytes, Data = resultData };
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction InitPlayerAccountForSeason(InitPlayerAccountForSeasonAccounts accounts, ushort seasonCount, string playerId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Verifier, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.VerifierAccount, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameSeason, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(11669650370663511056UL, offset);
                offset += 8;
                _data.WriteU16(seasonCount, offset);
                offset += 2;
                offset += _data.WriteBorshString(playerId, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction { Keys = keys, ProgramId = ScorekeeperClient.ProgramId.KeyBytes, Data = resultData };
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction IncrementPlayerScore(IncrementPlayerScoreAccounts accounts, ushort seasonCount, string playerId, ulong increment)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Verifier, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.VerifierAccount, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.GameSeason, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9068587049042107212UL, offset);
                offset += 8;
                _data.WriteU16(seasonCount, offset);
                offset += 2;
                offset += _data.WriteBorshString(playerId, offset);
                _data.WriteU64(increment, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction { Keys = keys, ProgramId = ScorekeeperClient.ProgramId.KeyBytes, Data = resultData };
            }
        }
    }
}
