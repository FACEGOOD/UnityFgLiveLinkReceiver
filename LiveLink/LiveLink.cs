using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using Newtonsoft.Json.Linq;

public class Property
{
    public string Name { get; set; }
    public float Value { get; set; }
}

public class PropertiesObject
{
    public List<Property> Properties { get; set; }
}

namespace FaceGood.LiveLink
{
    [AddComponentMenu("FACEGOOD/" + nameof(LiveLink))]
    public class LiveLink : MonoBehaviour
    {
        public bool joinMulticast = false;
        public string multicaseAddress = "224.0.1.0";
        public int port = 54321;
        public string connectState = "No connection!";

    [CustomEditor(typeof(LiveLink))]
    public class LiveLinkEditor : Editor
    {
        SerializedProperty joinMulticast;
        SerializedProperty multicaseAddress;
        SerializedProperty port;
        SerializedProperty connectState;

        private void OnEnable()
        {
            Debug.Log("OnEnable:");
            joinMulticast = serializedObject.FindProperty("joinMulticast");
            multicaseAddress = serializedObject.FindProperty("multicaseAddress");
            port = serializedObject.FindProperty("port");
            connectState = serializedObject.FindProperty("connectState");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(joinMulticast);
            if (joinMulticast.boolValue)
            {
                EditorGUILayout.PropertyField(multicaseAddress);
            }

            EditorGUILayout.PropertyField(port);
            EditorGUILayout.PropertyField(connectState);

            serializedObject.ApplyModifiedProperties();
        }
    }

        private string objectName;
        private SkinnedMeshRenderer MeshRenderer;
        private Dictionary<string, int> MeshNameIndexDict = new Dictionary<string, int>();

        private ConcurrentQueue<Dictionary<string, float>> concurrentQueue;

        private UdpClient udpClient;
        private Thread receiveThread;
        private bool isReceiving = false;

        private void Awake()
        {
            try
            {
                objectName = gameObject.name;
                MeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                Mesh SharedMesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
                int BlenderShapeCount = SharedMesh.blendShapeCount;
                for (int i = 0; i < BlenderShapeCount; i++)
                {
                    int MeshNameLength = SharedMesh.GetBlendShapeName(i).Split('.').Length;
                    string MeshName = SharedMesh.GetBlendShapeName(i).Split('.')[MeshNameLength - 1];
                    MeshNameIndexDict.Add(MeshName.ToLower(), i);
                }
            }
            catch (MissingComponentException)
            {
                Debug.Log($"{objectName} doesn't have the component SkinnedMeshRenderer");
            }
            
        }

        void Start()
        {
            Debug.Log("Start...");
            udpClient = new UdpClient();
            try
            {
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                if (joinMulticast)
                {
                    udpClient.JoinMulticastGroup(IPAddress.Parse(multicaseAddress));
                }
            }
            catch (SocketException ex)
            {
                Debug.Log("Failed to join muticast group:" + multicaseAddress + ",error:" + ex.Message);
                udpClient.Close();
            }

            concurrentQueue = new ConcurrentQueue<Dictionary<string, float>>();

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            isReceiving = true;
        }

        void Update()
        {
            Dictionary<string, float> FrameData;
            if (concurrentQueue != null && concurrentQueue.TryDequeue(out FrameData))
            {
                connectState = "Connected";
                foreach (KeyValuePair<string, float> kvp in FrameData)
                {
                    try
                    {
                        int MeshNameMapIndex = MeshNameIndexDict[kvp.Key];
                        MeshRenderer.SetBlendShapeWeight(MeshNameMapIndex, kvp.Value * 100.0f);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (udpClient != null)
            {
                udpClient.Close();
            }
            isReceiving = false;
            receiveThread.Abort();
        }

        private void ReceiveData()
        {
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
            while (isReceiving)
            {
                try
                {
                    byte[] data = udpClient.Receive(ref anyIP);
                    string message = Encoding.UTF8.GetString(data);

                    JObject jsonObj = JObject.Parse(message);
                    JProperty jProperty = jsonObj.Properties().FirstOrDefault();
                    if (jProperty.Name != objectName)
                    {
                        Debug.Log("subjectName:" + jProperty.Name);
                        continue;
                    }

                    PropertiesObject porpertyObject = jProperty.Value.ToObject<PropertiesObject>();

                    Dictionary<string, float> FrameData = new Dictionary<string, float>();
                    foreach (Property property in porpertyObject.Properties)
                    {
                        FrameData[property.Name.ToLower()] = property.Value;
                    }

                    concurrentQueue.Enqueue(FrameData);
                }
                catch (Exception e)
                {
                    connectState = "No connection!";
                    break;
                }
            }

            connectState = "No connection!";
        }
    }
}
