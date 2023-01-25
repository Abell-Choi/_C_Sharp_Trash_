using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UDP_Connector {
    class UDP_Connector {
        UdpClient _conn = new UdpClient(9999);                      // UDP Connector
        IPEndPoint _rEP = null;                                     // IP ENDPOINT
        bool isRunning = false;                                     // Server Switch

        string conn_key = string.Empty;                             // Connection Auth Key

        Thread recv_th = null;                                      // recv_runner_thread
        double recv_await_delay = .5;                               // recv async delay 

        public delegate void Receive_Event ( JObject jobject );
        public EventHandler Recv_Event;
        public class Receive_Event_Argument : EventArgs {
            public string log_string { get; }
            public JObject json_JObject { get; }
            public Receive_Event_Argument ( string log, JObject j ) {
                this.json_JObject = j;
                this.log_string = log;
            }
        }
        public UDP_Connector ( ) {
            isRunning = true;
            Thread t = new Thread(new ThreadStart(delegate(){
                var _event_args=  new Receive_Event_Argument("service start", null);
                Recv_Event( this, _event_args );                                                               // service start 알림
                while( isRunning ){                                                                     // 서버 생존까지 무한반복
                    Thread.Sleep((int)(1000 * this.recv_await_delay));
                    var recv = recv_async_handler();
                    if (recv == null){ continue; }
                    int _port = recv.Value.RemoteEndPoint.Port;
                    var recv_byte = recv.Value.Buffer;
                    var recv_string = byte_to_string(recv_byte);
                    var recv_jobject = string_to_jobject(recv_string);
                    if (!recv_jobject.ContainsKey("TYPE")){continue; }

                    string _key = recv_jobject.Value<string>("AUTH");
                    if (check_key(_key) == 0){                                                          // Key 안맞음
                        _event_args = new Receive_Event_Argument ("Key 이미 존재", null);
                        var _auth_deny_map =  new Dictionary<string, string>(){
                            { "TYPE" , "AUTH_DENY"},
                            {"VALUE", "" },
                            {"AUTH",  _key}
                        };
                        send_message(JsonConvert.SerializeObject(_auth_deny_map), _port);
                        Recv_Event(this, _event_args);
                        continue;
                    }
                    if (set_connection_key(_key) == 0){                                                 // Key 셋팅 실패
                        _event_args = new Receive_Event_Argument("Key 셋팅 안됨", null);
                        var _auth_deny_map =  new Dictionary<string, string>(){
                            { "TYPE" , "AUTH_DENY"},
                            {"VALUE", "" },
                            {"AUTH",  _key}
                        };
                        send_message(JsonConvert.SerializeObject(_auth_deny_map), _port);
                        Recv_Event(this, _event_args);
                        continue;
                    }else{
                        conn_key = _key;
                        this._rEP = new IPEndPoint(IPAddress.Loopback, _port);
                        _event_args = new Receive_Event_Argument("KEY 셋팅됨 -> " +this.conn_key, null);
                        Recv_Event(this, _event_args);
                        var _auth_confirm =  new Dictionary<string, string>(){
                            { "TYPE" , "AUTH_CONFIRM"},
                            {"VALUE", _key},
                            {"AUTH",  _key}
                        };
                        send_message(JsonConvert.SerializeObject(_auth_confirm), _port);
                    }

                    // event send
                    _event_args = new Receive_Event_Argument (recv_string, recv_jobject);
                    Recv_Event(this,_event_args);
                }
            } ));
            recv_th = t;
            t.Start();
        }

        /// <summary> Running RECV function </summary>
        private UdpReceiveResult? recv_async_handler ( ) {
            if ( !isRunning ) { return null; }
            var recv_data = _conn.ReceiveAsync();
            while ( !recv_data.IsCompleted ) {
                Thread.Sleep( ( int ) ( recv_await_delay * 1000 ) );
                if ( !isRunning ) { return null; }                       // if server down, thread loop break;
            }

            return recv_data.Result;
        }

        /// <summary> Byte -> string </summary>
        string byte_to_string ( byte [ ] data ) {
            try { return Encoding.Default.GetString( data ); } catch ( Exception e ) { return "err|" + e.ToString(); }

        }

        JObject string_to_jobject ( string data ) {
            try { return JObject.Parse( data ); } catch ( Exception e ) { return null; }
        }

        /// <summary> Key value checker -1 : no key setup, 0 : not match, 1: match</summary>
        public int check_key ( string key ) {
            if ( this.conn_key == string.Empty ) { return -1; }
            if ( this.conn_key != key ) { return 0; }
            return 1;
        }

        /// <summary key setup 0 : already key setup, 1: done </summary>
        public int set_connection_key ( string key ) {
            if ( this.conn_key != string.Empty ) { return 0; }      // key setting already
            this.conn_key = key;                                    // key setup
            return 1;
        }

        /// <summary>
        /// Message 를 전달합니다. Json으로 전달되어야하는 코드입니다.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public int send_message ( string message, int port = -1 ) {

            if ( !isRunning ) { return -1; }
            if(_rEP == null) { return -2; }
            if ( this._rEP.Port == 0 && port == -1 ) { return -2; }

            if ( port == -1 ) { port = this._rEP.Port; }


            byte[] msg = Encoding.UTF8.GetBytes(message);
            var _rep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            try {
                this._conn.Send( msg, msg.Length, _rep );
            } catch ( Exception e ) {
                return 0;
            }
            return 1;

        }

        public void stop_server ( ) {
            //this.recv_th.Interrupt();
            Dictionary<string, string> _end_proc = new Dictionary<string, string>();
            _end_proc.Add( "TYPE", "EOF" );
            _end_proc.Add( "VALUE", "EOF" );
            _end_proc.Add( "AUTH", conn_key );
            send_message( JsonConvert.SerializeObject( _end_proc ) );
            this.isRunning = false;
            this.recv_th.Abort();
        }
    }
}