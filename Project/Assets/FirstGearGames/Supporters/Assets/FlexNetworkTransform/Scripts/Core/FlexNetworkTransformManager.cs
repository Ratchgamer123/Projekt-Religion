using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace FirstGearGames.Mirrors.Assets.FlexNetworkTransforms
{
    public struct TransformSyncDataMessage : NetworkMessage
    {
        public ushort SequenceId;
        public List<TransformSyncData> Data;
    }

    public class FlexNetworkTransformManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Active FlexNetworkTransform components.
        /// </summary>
        private static List<FlexNetworkTransformBase> _activeFlexNetworkTransforms = new List<FlexNetworkTransformBase>();
        /// <summary>
        /// Unreliable SyncDatas to send to all.
        /// </summary>
        private static List<TransformSyncData> _toAllUnreliableSyncData = new List<TransformSyncData>();
        /// <summary>
        /// Reliable SyncDatas to send to all.
        /// </summary>
        private static List<TransformSyncData> _toAllReliableSyncData = new List<TransformSyncData>();
        /// <summary>
        /// Unreliable SyncDatas to send to server.
        /// </summary>
        private static List<TransformSyncData> _toServerUnreliableSyncData = new List<TransformSyncData>();
        /// <summary>
        /// Reliable SyncDatas to send send to server.
        /// </summary>
        private static List<TransformSyncData> _toServerReliableSyncData = new List<TransformSyncData>();
        /// <summary>
        /// Unreliable SyncDatas sent to specific observers.
        /// </summary>
        private static Dictionary<NetworkConnection, List<TransformSyncData>> _observerUnreliableSyncData = new Dictionary<NetworkConnection, List<TransformSyncData>>();
        /// <summary>
        /// Reliable SyncDatas sent to specific observers.
        /// </summary>
        private static Dictionary<NetworkConnection, List<TransformSyncData>> _observerReliableSyncData = new Dictionary<NetworkConnection, List<TransformSyncData>>();
        /// <summary>
        /// True if a fixed frame.
        /// </summary>
        private bool _fixedFrame = false;
        /// <summary>
        /// Last fixed frame.
        /// </summary>
        private int _lastFixedFrame = -1;
        /// <summary>
        /// Last sequenceId sent by server.
        /// </summary>
        private ushort _lastServerSentSequenceId = 0;
        /// <summary>
        /// Last sequenceId received from server.
        /// </summary>
        private ushort _lastServerReceivedSequenceId = 0;
        /// <summary>
        /// Last sequenceId sent by this client.
        /// </summary>
        private ushort _lastClientSentSequenceId = 0;
        /// <summary>
        /// Last NetworkClient.active state.
        /// </summary>
        private bool _lastClientActive = false;
        /// <summary>
        /// Last NetworkServer.active state.
        /// </summary>
        private bool _lastServerActive = false;
        /// <summary>
        /// How much data can be bundled per reliable message.
        /// </summary>
        private int _reliableDataBundleCount = -1;
        /// <summary>
        /// How much data can be bundled per unreliable message.
        /// </summary>
        private int _unreliableDataBundleCount = -1;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum possible size for a sync data. This value is intentionally larger than neccessary.
        /// </summary>
        private const int MAXIMUM_DATA_SIZE = 40;
        /// <summary>
        /// Maximum packet size by default. This is used when packet size is unknown.
        /// </summary>
        private const int DEFAULT_MAXIMUM_PACKET_SIZE = 1200;
        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FirstInitialize()
        {
            GameObject go = new GameObject();
            go.name = "FlexNetworkTransformManager";
            go.AddComponent<FlexNetworkTransformManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            StartCoroutine(__SetDataBundleCount());
        }

        private void FixedUpdate()
        {
            /* Don't send if the same frame. Since
            * physics aren't actually involved there is
            * no reason to run logic twice on the
            * same frame; that will only hurt performance
            * and the network more. */
            if (Time.frameCount == _lastFixedFrame)
                return;
            _lastFixedFrame = Time.frameCount;

            _fixedFrame = true;
        }


        private void Update()
        {
            CheckRegisterHandlers();

            //Run updates on FlexNetworkTransforms.
            for (int i = 0; i < _activeFlexNetworkTransforms.Count; i++)
                _activeFlexNetworkTransforms[i].ManualUpdate(_fixedFrame);

            _fixedFrame = false;
            //Send any queued messages.
            SendMessages();
        }

        /// <summary>
        /// Calculates data bundle count.
        /// </summary>
        private IEnumerator __SetDataBundleCount()
        {
            //Immediately set using default packet size.
            CalculateDataBundleCount(DEFAULT_MAXIMUM_PACKET_SIZE, DEFAULT_MAXIMUM_PACKET_SIZE);

            //Give up after 10 seconds of trying.
            float timeout = Time.unscaledTime + 10f;
            while (Transport.activeTransport == null)
            {
                //If timed out then exit coroutine.
                if (Time.unscaledTime > timeout)
                {
                    Debug.LogWarning("Could not locate transport being used, unable to calculate DataBundleCount. If client only you may ignore this message.");
                    yield break;
                }

                yield return null;
            }

            int reliableSize = Transport.activeTransport.GetMaxPacketSize(0);
            int unreliableSize = Transport.activeTransport.GetMaxPacketSize(1);
            CalculateDataBundleCount(reliableSize, unreliableSize);
        }

        /// <summary>
        /// Sets roughly how many datas can send per bundle.
        /// </summary>
        private void CalculateDataBundleCount(int reliableMaxPacketSize, int unreliableMaxPacketSize)
        {
            //High value since it's unknown.
            int headerSize = 20;

            _reliableDataBundleCount = (reliableMaxPacketSize - headerSize) / MAXIMUM_DATA_SIZE;
            _unreliableDataBundleCount = (unreliableMaxPacketSize - headerSize) / MAXIMUM_DATA_SIZE;
        }

        /// <summary>
        /// Registers handlers for the client.
        /// </summary>
        private void CheckRegisterHandlers()
        {
            bool changed = (_lastClientActive != NetworkClient.active || _lastServerActive != NetworkServer.active);
            //If wasn't active previously but is now then get handlers again.
            if (changed && NetworkClient.active)
                NetworkClient.ReplaceHandler<TransformSyncDataMessage>(OnServerTransformSyncData);
            if (changed && NetworkServer.active)
                NetworkServer.ReplaceHandler<TransformSyncDataMessage>(OnClientTransformSyncData);

            _lastServerActive = NetworkServer.active;
            _lastClientActive = NetworkClient.active;
        }

        /// <summary>
        /// Adds to ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="fntBase"></param>
        public static void AddToActive(FlexNetworkTransformBase fntBase)
        {
            _activeFlexNetworkTransforms.Add(fntBase);
        }
        /// <summary>
        /// Removes from ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="fntBase"></param>
        public static void RemoveFromActive(FlexNetworkTransformBase fntBase)
        {
            _activeFlexNetworkTransforms.Remove(fntBase);
        }

        /// <summary>
        /// Sends data to server.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Client]
        public static void SendToServer(TransformSyncData data, bool reliable)
        {
            List<TransformSyncData> list = (reliable) ? _toServerReliableSyncData : _toServerUnreliableSyncData;
            list.Add(data);
        }

        /// <summary>
        /// Sends data to all.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Server]
        public static void SendToAll(TransformSyncData data, bool reliable)
        {
            List<TransformSyncData> list = (reliable) ? _toAllReliableSyncData : _toAllUnreliableSyncData;
            list.Add(data);
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Server]
        public static void SendToObserver(NetworkConnection conn, TransformSyncData data, bool reliable)
        {
            Dictionary<NetworkConnection, List<TransformSyncData>> dict = (reliable) ? _observerReliableSyncData : _observerUnreliableSyncData;

            List<TransformSyncData> datas;
            //If doesn't have datas for connection yet then make new datas.
            if (!dict.TryGetValue(conn, out datas))
            {
                datas = new List<TransformSyncData>();
                dict[conn] = datas;
            }

            datas.Add(data);
        }

        /// <summary>
        /// Sends queued messages.
        /// </summary>
        private void SendMessages()
        {
            //Server.
            if (NetworkServer.active)
            {
                _lastServerSentSequenceId++;
                if (_lastServerSentSequenceId == ushort.MaxValue)
                    _lastServerSentSequenceId = 0;

                //Reliable to all.
                SendTransformSyncDatas(_lastServerSentSequenceId, false, null, _toAllReliableSyncData, true);
                //Unreliable to all.
                SendTransformSyncDatas(_lastServerSentSequenceId, false, null, _toAllUnreliableSyncData, false);
                //Reliable to observers.
                foreach (KeyValuePair<NetworkConnection, List<TransformSyncData>> item in _observerReliableSyncData)
                {
                    //Null or unready network connection.
                    if (item.Key == null || !item.Key.isReady)
                        continue;

                    SendTransformSyncDatas(_lastServerSentSequenceId, false, item.Key, item.Value, true);
                }
                //Unreliable to observers.
                foreach (KeyValuePair<NetworkConnection, List<TransformSyncData>> item in _observerUnreliableSyncData)
                {
                    //Null or unready network connection.
                    if (item.Key == null || !item.Key.isReady)
                        continue;

                    SendTransformSyncDatas(_lastServerSentSequenceId, false, item.Key, item.Value, false);
                }
            }
            //Client.
            if (NetworkClient.active)
            {
                _lastClientSentSequenceId++;
                if (_lastClientSentSequenceId == ushort.MaxValue)
                    _lastClientSentSequenceId = 0;

                //Reliable to all.
                SendTransformSyncDatas(_lastClientSentSequenceId, true, null, _toServerReliableSyncData, true);
                //Unreliable to all.
                SendTransformSyncDatas(_lastClientSentSequenceId, true, null, _toServerUnreliableSyncData, false);
            }

            _toServerReliableSyncData.Clear();
            _toServerUnreliableSyncData.Clear();
            _toAllReliableSyncData.Clear();
            _toAllUnreliableSyncData.Clear();
            _observerReliableSyncData.Clear();
            _observerUnreliableSyncData.Clear();
        }

        /// <summary>
        /// Sends data to all or specified connection.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="datas"></param>
        /// <param name="reliable"></param>
        /// <param name="maxCollectionSize"></param>
        private void SendTransformSyncDatas(ushort sequenceId, bool toServer, NetworkConnection conn, List<TransformSyncData> datas, bool reliable)
        {
            int index = 0;
            int bundleCount = (reliable) ? _reliableDataBundleCount : _unreliableDataBundleCount;
            int channel = (reliable) ? 0 : 1;
            while (index < datas.Count)
            {
                int count = Mathf.Min(bundleCount, datas.Count - index);
                TransformSyncDataMessage msg = new TransformSyncDataMessage()
                {
                    SequenceId = sequenceId,
                    Data = datas.GetRange(index, count)
                };

                if (toServer)
                {
                    NetworkClient.Send(msg, channel);
                }
                else
                {
                    //If no connection then send to all.
                    if (conn == null)
                        NetworkServer.SendToAll(msg, channel);
                    //Otherwise send to connection.
                    else
                        conn.Send(msg, channel);
                }
                index += count;
            }
        }

        /// <summary>
        /// Received on clients when server sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnServerTransformSyncData(TransformSyncDataMessage msg)
        {
            //Old packet.
            if (IsOldPacket(_lastServerReceivedSequenceId, msg.SequenceId))
                return;

            _lastServerReceivedSequenceId = msg.SequenceId;

            int count = msg.Data.Count;
            for (int i = 0; i < count; i++)
            {
                /* Initially I tried caching the getcomponent calls but the performance difference
                 * couldn't be registered. At this time it's not worth creating the extra complexity
                 * for what might be a 1% fps difference. */
                if (NetworkIdentity.spawned.TryGetValue(msg.Data[i].NetworkIdentity, out NetworkIdentity ni))
                {
                    if (ni != null)
                    {
                        FlexNetworkTransformBase fntBase = ReturnFNTBaseOnNetworkIdentity(ni, msg.Data[i].ComponentIndex);
                        if (fntBase != null)
                            fntBase.ServerDataReceived(msg.Data[i]);
                    }
                }
            }

        }

        /// <summary>
        /// Received on server when client sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnClientTransformSyncData(TransformSyncDataMessage msg)
        {
            //Have to check sequence id against the FNT sending.
            int count = msg.Data.Count;
            for (int i = 0; i < count; i++)
            {
                /* Initially I tried caching the getcomponent calls but the performance difference
                * couldn't be registered. At this time it's not worth creating the extra complexity
                * for what might be a 1% fps difference. */
                if (NetworkIdentity.spawned.TryGetValue(msg.Data[i].NetworkIdentity, out NetworkIdentity ni))
                {
                    if (ni != null)
                    {
                        FlexNetworkTransformBase fntBase = ReturnFNTBaseOnNetworkIdentity(ni, msg.Data[i].ComponentIndex);
                        if (fntBase != null)
                        {
                            //Skip if old packet.
                            if (IsOldPacket(fntBase.LastClientSequenceId, msg.SequenceId))
                                continue;

                            fntBase.SetLastClientSequenceIdInternal(msg.SequenceId);
                            fntBase.ClientDataReceived(msg.Data[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a FlexNetworkTransformBase on a networkIdentity using a componentIndex.
        /// </summary>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        private FlexNetworkTransformBase ReturnFNTBaseOnNetworkIdentity(NetworkIdentity ni, byte componentIndex)
        {
            /* Networkbehaviours within the collection are the same order as compenent indexes.
            * I can save several iterations by simply grabbing the index from the networkbehaviours collection rather than iterating
            * it. */
            //A network behaviour was removed or added at runtime, component counts don't match up.
            if (componentIndex >= ni.NetworkBehaviours.Length)
                return null;

            FlexNetworkTransformBase[] fntBases = ni.NetworkBehaviours[componentIndex].GetComponents<FlexNetworkTransformBase>();
            /* Now find the FNTBase which matches the component index. There is probably only one FNT
             * but if the user were using FNT + FNT Child there could be more so it's important to get all FNT
             * on the object. */
            for (int i = 0; i < fntBases.Length; i++)
            {
                //Match found.
                if (fntBases[i].CachedComponentIndex == componentIndex)
                    return fntBases[i];
            }

            /* If here then the component index was found but the fnt with the component index
             * was not. This should never happen. */
            Debug.LogWarning("ComponentIndex found but FlexNetworkTransformBase was not.");
            return null;
        }


        /// <summary>
        /// Returns if a packet is old or out of order.
        /// </summary>
        /// <param name="lastSequenceId"></param>
        /// <param name="sequenceId"></param>
        /// <returns></returns>
        private bool IsOldPacket(ushort lastSequenceId, ushort sequenceId, ushort resetRange = 256)
        {
            /* New Id is equal or higher. Allow equal because
             * the same sequenceId will be used for when bundling
             * hundreds of FNTs over multiple sends. */
            if (sequenceId >= lastSequenceId)
            {
                return false;
            }
            //New sequenceId isn't higher, check if perhaps the sequenceId reset to 0.
            else
            {
                ushort difference = (ushort)Mathf.Abs(lastSequenceId - sequenceId);
                /* Return old packet if difference isnt beyond
                 * the reset range. Difference should be extreme if a reset occurred. */
                return (difference < resetRange);
            }
        }

    }


}