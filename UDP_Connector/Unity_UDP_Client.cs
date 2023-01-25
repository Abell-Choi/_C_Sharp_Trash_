using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System;
using System.Data;

public class UDP_Connector : MonoBehaviour {
    UdpClient _conn = new UdpClient();
    int _default_port = 9999;
    double await_delay_time = 0.5;

    bool isRunning = false;
    public string auth_key = "ajdskfljaslfkjasdkldf";
    bool quit_signal = false;

    Thread recv_th;
    void Start ( ) {
        isRunning = true;
        // send init
        Dictionary<string, string> _data = new Dictionary<string, string>();
        _data.Add( "TYPE", "AUTH" );
        _data.Add( "VALUE", auth_key );
        _data.Add( "AUTH", auth_key );
        send_message( JsonConvert.SerializeObject( _data ) );


        Thread t = new Thread(new ThreadStart(delegate(){
            while(isRunning){
                Thread.Sleep((int)(1000 * await_delay_time));
                var recv = recv_async_handler();
                if (recv == null){continue; }
                var recv_byte = recv.Value.Buffer;
                var recv_string = byte_to_string(recv_byte);
                var recv_jobject = string_to_jobject(recv_string);

                Debug.Log(recv_string);
                if (recv_jobject.Value<string>("TYPE") == "EOF"){quit_signal = true; return; }          // WF 종료
                if (recv_jobject.Value<string>("TYPE") == "AUTH_DENY"){quit_signal = true; return; }    // 키 안맞음
            }
        } ));

        recv_th = t;
        t.Start();
    }

    private void Update ( ) {
        if ( quit_signal ) {
            Debug.Log( quit_signal.ToString() );
            UnityEditor.EditorApplication.isPlaying = false;
            Application.Quit(); 
        }
    }

    private void OnDestroy ( ) {
        recv_th.Abort();
    }

    /// <summary> Running RECV function </summary>
    UdpReceiveResult? recv_async_handler ( ) {
        if ( !isRunning ) { return null; }
        var recv_data = _conn.ReceiveAsync();
        while ( !recv_data.IsCompleted ) {
            Thread.Sleep( ( int ) ( 1000 * await_delay_time ) );
            if ( !isRunning ) { return null; }
        }
        try {
            return recv_data.Result;
        }catch(Exception e ) {
            Debug.Log( e.ToString() );
            return null;
        }
    }


    /// <summary>
    /// 메시지 전달을 위한 코드
    /// </summary>
    /// <param name="message"></param>
    /// <param name="port"></param>
    void send_message ( string message, int port = -1 ) {
        if ( port == -1 ) { port = _default_port; }
        IPEndPoint _rep = new IPEndPoint(IPAddress.Loopback, port);
        var _byte = Encoding.UTF8.GetBytes(message);
        _conn.Send( _byte, _byte.Length, _rep );
    }

    void send_click_data ( string [ ] cell_code_array ) {
        Dictionary<string, dynamic> _map = new Dictionary<string, dynamic>();
        _map.Add( "TYPE", "CELL_CLICK" );
        _map.Add( "AUTH", this.auth_key );
        _map.Add( "VALUE", cell_code_array );
        string json_data = JsonConvert.SerializeObject(_map);
        send_message( json_data );
    }

    /// <summary> Byte -> string </summary>
    string byte_to_string ( byte [ ] data ) {
        try { return Encoding.Default.GetString( data ); } catch ( Exception e ) { return "err|" + e.ToString(); }

    }
    /// <summary> string -> JObject </summary>
    JObject string_to_jobject ( string data ) {
        try { return JObject.Parse( data ); } catch ( Exception e ) { return null; }
    }
}
