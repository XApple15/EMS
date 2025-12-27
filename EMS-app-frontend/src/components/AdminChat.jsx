// Admin Dashboard - Chat with Clients Component
import React, { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';

const AdminChat = () => {
    const [connection, setConnection] = useState(null);
    const [adminId, setAdminId] = useState(null);
    const [chatSessions, setChatSessions] = useState([]);
    const [pendingChats, setPendingChats] = useState([]);
    const [selectedSession, setSelectedSession] = useState(null);
    const [messages, setMessages] = useState([]);
    const [inputMessage, setInputMessage] = useState('');
    const [connectionStatus, setConnectionStatus] = useState('disconnected');
    const [showNewChatModal, setShowNewChatModal] = useState(false);
    const [newChatClientId, setNewChatClientId] = useState('');
    const [newChatMessage, setNewChatMessage] = useState('');
    const [activeTab, setActiveTab] = useState('active'); // 'active' or 'pending'

    const messagesEndRef = useRef(null);
    const connectionRef = useRef(null);

    useEffect(() => {
        // Get admin ID from localStorage
        const loadAdminId = async () => {
            try {
                const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
                if (storedUser && (storedUser.id || storedUser.Id)) {
                    const authId = storedUser.id ?? storedUser.Id;

                    // Fetch the actual user data to get the database user ID
                    const { userService } = await import('../api/api-service');
                    const userData = await userService.getUserByAuthId(authId);

                    if (userData && (userData.id || userData.Id)) {
                        const dbUserId = userData.id ?? userData.Id;
                        setAdminId(dbUserId);
                        console.log('Admin Database User ID loaded:', dbUserId);
                    } else {
                        console.warn('Could not load database user ID, using auth ID');
                        setAdminId(authId);
                    }
                }
            } catch (err) {
                console.error('Error loading admin ID:', err);
                const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
                if (storedUser && (storedUser.id || storedUser.Id)) {
                    setAdminId(storedUser.id ?? storedUser.Id);
                }
            }
        };

        loadAdminId();
    }, []);

    useEffect(() => {
        if (!adminId) return;

        // Create SignalR connection
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl('http://websocket.docker.localhost/chathub', {
                skipNegotiation: false,
                withCredentials: true,
                transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Debug)
            .build();

        connectionRef.current = newConnection;

        // Connection established
        newConnection.on('Connected', async (connectionId) => {
            console.log('Admin connected:', connectionId);
            setConnectionStatus('connected');

            try {
                // Register as admin
                await newConnection.invoke('RegisterUser', adminId, 'admin');
                console.log('Registered as admin:', adminId);
            } catch (err) {
                console.error('Error registering admin:', err);
            }
        });

        // Registration confirmation
        newConnection.on('Registered', (data) => {
            console.log('Admin registration confirmed:', data);
        });

        // Joined chat room confirmation
        newConnection.on('JoinedChatRoom', (chatRoomId) => {
            console.log('Joined chat room:', chatRoomId);
        });

        // Admin chat message sent confirmation
        newConnection.on('AdminChatMessageSent', (message) => {
            console.log('Admin message sent:', message);

            // Add to local messages
            setMessages(prev => [...prev, {
                messageId: message.messageId || Date.now(),
                senderId: message.senderId,
                senderRole: 'admin',
                message: message.message,
                timestamp: message.timestamp,
                chatRoomId: message.chatRoomId
            }]);
        });

        // Receive admin chat message
        newConnection.on('ReceiveAdminChatMessage', (message) => {
            console.log('Received admin chat message:', message);

            setMessages(prev => {
                // Avoid duplicates
                if (prev.some(m => m.messageId === message.messageId)) {
                    return prev;
                }
                return [...prev, message];
            });

            // Update session with new message
            setChatSessions(prev => prev.map(session =>
                session.chatRoomId === message.chatRoomId
                    ? {
                        ...session,
                        lastMessage: message.message,
                        lastMessageAt: message.timestamp,
                        unreadCount: selectedSession?.chatRoomId === message.chatRoomId ? 0 : session.unreadCount + 1
                    }
                    : session
            ));
        });

        // Receive admin notification (new chat requests)
        newConnection.on('ReceiveAdminNotification', (notification) => {
            console.log('Received admin notification:', notification);

            if (notification.data?.NotificationType === 'NewChatRequest') {
                // Reload pending chats
                loadPendingChats();

                // Show browser notification
                if ('Notification' in window && Notification.permission === 'granted') {
                    new Notification(notification.title, {
                        body: notification.message,
                        icon: '/admin-icon.png'
                    });
                }
            }
        });

        // Error handling
        newConnection.on('Error', (message) => {
            console.error('SignalR Error:', message);
        });

        newConnection.onreconnecting(() => {
            console.log('Reconnecting...');
            setConnectionStatus('connecting');
        });

        newConnection.onreconnected(async () => {
            console.log('Reconnected');
            setConnectionStatus('connected');

            try {
                await newConnection.invoke('RegisterUser', adminId, 'admin');

                // Rejoin chat room if active
                if (selectedSession) {
                    await newConnection.invoke('JoinChatRoom', selectedSession.chatRoomId);
                }
            } catch (err) {
                console.error('Error re-registering admin:', err);
            }
        });

        newConnection.onclose(() => {
            console.log('Connection closed');
            setConnectionStatus('disconnected');
        });

        newConnection.start()
            .then(() => {
                console.log('Admin SignalR connected');
                setConnection(newConnection);
                loadChatSessions();
                loadPendingChats();

                // Request notification permission
                if ('Notification' in window && Notification.permission === 'default') {
                    Notification.requestPermission();
                }
            })
            .catch(err => console.error('Connection error:', err));

        return () => {
            if (selectedSession && newConnection.state === signalR.HubConnectionState.Connected) {
                newConnection.invoke('LeaveChatRoom', selectedSession.chatRoomId).catch(console.error);
            }
            newConnection.stop();
        };
    }, [adminId]);

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    const loadChatSessions = async () => {
        if (!adminId) return;

        try {
            const response = await axios.get(`http://customersupport.docker.localhost/api/AdminChat/sessions/admin/${adminId}`);
            if (response.data.success) {
                setChatSessions(response.data.sessions);
            }
        } catch (error) {
            console.error('Error loading sessions:', error);
        }
    };

    const loadPendingChats = async () => {
        try {
            const response = await axios.get('http://customersupport.docker.localhost/api/AdminChat/pending');
            if (response.data.success) {
                setPendingChats(response.data.sessions);
            }
        } catch (error) {
            console.error('Error loading pending chats:', error);
        }
    };

    const loadMessages = async (chatRoomId) => {
        try {
            const response = await axios.get(`http://customersupport.docker.localhost/api/AdminChat/messages/${chatRoomId}`);
            if (response.data.success) {
                setMessages(response.data.messages);
                markAsRead(chatRoomId);
            }
        } catch (error) {
            console.error('Error loading messages:', error);
        }
    };

    const markAsRead = async (chatRoomId) => {
        if (!adminId) return;

        try {
            await axios.post(`http://customersupport.docker.localhost/api/AdminChat/read/${chatRoomId}/${adminId}`);

            // Update local state
            setChatSessions(prev => prev.map(session =>
                session.chatRoomId === chatRoomId
                    ? { ...session, unreadCount: 0 }
                    : session
            ));
        } catch (error) {
            console.error('Error marking as read:', error);
        }
    };

    const selectSession = async (session) => {
        // Leave previous chat room
        if (selectedSession && connection) {
            try {
                await connection.invoke('LeaveChatRoom', selectedSession.chatRoomId);
            } catch (err) {
                console.error('Error leaving chat room:', err);
            }
        }

        setSelectedSession(session);
        loadMessages(session.chatRoomId);

        // Join new chat room
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            try {
                await connection.invoke('JoinChatRoom', session.chatRoomId);
            } catch (err) {
                console.error('Error joining chat room:', err);
            }
        }
    };

    const acceptPendingChat = async (chat) => {
        if (!adminId) return;

        try {
            const response = await axios.post(`http://customersupport.docker.localhost/api/AdminChat/assign/${chat.chatRoomId}/${adminId}`);

            if (response.data.success) {
                // Remove from pending
                setPendingChats(prev => prev.filter(c => c.chatRoomId !== chat.chatRoomId));

                // Add to active sessions
                setChatSessions(prev => [response.data.session, ...prev]);

                // Select the new session
                setSelectedSession(response.data.session);
                setActiveTab('active');

                // Join the chat room
                if (connection && connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke('JoinChatRoom', response.data.session.chatRoomId);
                }

                // Load messages
                loadMessages(response.data.session.chatRoomId);
            }
        } catch (error) {
            console.error('Error accepting chat:', error);
        }
    };

    const startNewChat = async () => {
        if (!newChatClientId.trim() || !adminId) return;

        try {
            const response = await axios.post('http://customersupport.docker.localhost/api/AdminChat/start', {
                adminId: adminId,
                clientId: newChatClientId,
                initialMessage: newChatMessage
            });

            if (response.data.success) {
                setChatSessions(prev => [response.data.session, ...prev]);
                setSelectedSession(response.data.session);
                setShowNewChatModal(false);
                setNewChatClientId('');
                setNewChatMessage('');
                setActiveTab('active');

                // Join the chat room
                if (connection && connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke('JoinChatRoom', response.data.session.chatRoomId);
                }

                loadMessages(response.data.session.chatRoomId);
            }
        } catch (error) {
            console.error('Error starting chat:', error);
        }
    };

    const sendMessage = async () => {
        if (!inputMessage.trim() || !selectedSession || !connection) return;

        try {
            const response = await axios.post('http://customersupport.docker.localhost/api/AdminChat/send', {
                chatRoomId: selectedSession.chatRoomId,
                senderId: adminId,
                senderRole: 'admin',
                message: inputMessage
            });

            if (response.data.success) {
                setInputMessage('');
            }
        } catch (error) {
            console.error('Error sending message:', error);
        }
    };

    const formatTime = (timestamp) => {
        const date = new Date(timestamp);
        return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
    };

    const formatDate = (timestamp) => {
        const date = new Date(timestamp);
        const now = new Date();
        const diff = Math.floor((now - date) / 1000);

        if (diff < 60) return 'Just now';
        if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
        if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    };

    return (
        <div style={{ display: 'flex', height: '100vh', background: '#f9fafb' }}>
            {/* Sessions List */}
            <div style={{ width: '320px', background: 'white', borderRight: '1px solid #e5e7eb', display: 'flex', flexDirection: 'column' }}>
                <div style={{ padding: '20px', borderBottom: '1px solid #e5e7eb' }}>
                    <h2 style={{ margin: '0 0 16px 0', fontSize: '20px', fontWeight: '600' }}>Admin Chat</h2>
                    <button
                        onClick={() => setShowNewChatModal(true)}
                        style={{
                            width: '100%',
                            padding: '10px',
                            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
                            color: 'white',
                            border: 'none',
                            borderRadius: '8px',
                            cursor: 'pointer',
                            fontWeight: '600',
                            marginBottom: '16px'
                        }}
                    >
                        + New Chat
                    </button>

                    {/* Tabs */}
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button
                            onClick={() => setActiveTab('active')}
                            style={{
                                flex: 1,
                                padding: '8px',
                                background: activeTab === 'active' ? '#9333ea' : '#f3f4f6',
                                color: activeTab === 'active' ? 'white' : '#6b7280',
                                border: 'none',
                                borderRadius: '6px',
                                cursor: 'pointer',
                                fontWeight: '600',
                                fontSize: '14px'
                            }}
                        >
                            Active ({chatSessions.length})
                        </button>
                        <button
                            onClick={() => setActiveTab('pending')}
                            style={{
                                flex: 1,
                                padding: '8px',
                                background: activeTab === 'pending' ? '#9333ea' : '#f3f4f6',
                                color: activeTab === 'pending' ? 'white' : '#6b7280',
                                border: 'none',
                                borderRadius: '6px',
                                cursor: 'pointer',
                                fontWeight: '600',
                                fontSize: '14px',
                                position: 'relative'
                            }}
                        >
                            Pending ({pendingChats.length})
                            {pendingChats.length > 0 && (
                                <span style={{
                                    position: 'absolute',
                                    top: '-4px',
                                    right: '-4px',
                                    width: '8px',
                                    height: '8px',
                                    background: '#ef4444',
                                    borderRadius: '50%',
                                    border: '2px solid white'
                                }}></span>
                            )}
                        </button>
                    </div>
                </div>

                <div style={{ flex: 1, overflowY: 'auto' }}>
                    {activeTab === 'active' ? (
                        // Active Chats
                        chatSessions.length === 0 ? (
                            <div style={{ padding: '20px', textAlign: 'center', color: '#9ca3af' }}>
                                No active chats
                            </div>
                        ) : (
                            chatSessions.map(session => (
                                <div
                                    key={session.chatRoomId}
                                    onClick={() => selectSession(session)}
                                    style={{
                                        padding: '16px',
                                        borderBottom: '1px solid #e5e7eb',
                                        cursor: 'pointer',
                                        background: selectedSession?.chatRoomId === session.chatRoomId ? '#f3f4f6' : 'white',
                                        transition: 'background 0.2s'
                                    }}
                                    onMouseEnter={(e) => {
                                        if (selectedSession?.chatRoomId !== session.chatRoomId) {
                                            e.currentTarget.style.background = '#f9fafb';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        if (selectedSession?.chatRoomId !== session.chatRoomId) {
                                            e.currentTarget.style.background = 'white';
                                        }
                                    }}
                                >
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                                        <strong style={{ fontSize: '14px' }}>
                                            {session.clientId || `Client ${session.chatRoomId.slice(-8)}`}
                                        </strong>
                                        {session.unreadCount > 0 && (
                                            <span style={{
                                                background: '#ef4444',
                                                color: 'white',
                                                borderRadius: '10px',
                                                padding: '2px 8px',
                                                fontSize: '12px',
                                                fontWeight: '600'
                                            }}>
                                                {session.unreadCount}
                                            </span>
                                        )}
                                    </div>
                                    <div style={{ fontSize: '13px', color: '#6b7280', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {session.lastMessage || 'No messages yet'}
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#9ca3af', marginTop: '4px' }}>
                                        {formatDate(session.lastMessageAt || session.startedAt)}
                                    </div>
                                </div>
                            ))
                        )
                    ) : (
                        // Pending Chats
                        pendingChats.length === 0 ? (
                            <div style={{ padding: '20px', textAlign: 'center', color: '#9ca3af' }}>
                                No pending requests
                            </div>
                        ) : (
                            pendingChats.map(chat => (
                                <div
                                    key={chat.chatRoomId}
                                    style={{
                                        padding: '16px',
                                        borderBottom: '1px solid #e5e7eb',
                                        background: '#fef3c7',
                                        transition: 'background 0.2s'
                                    }}
                                >
                                    <div style={{ marginBottom: '12px' }}>
                                        <strong style={{ fontSize: '14px', color: '#92400e' }}>
                                            New Request from {chat.clientId || `User ${chat.chatRoomId.slice(-8)}`}
                                        </strong>
                                        <div style={{ fontSize: '13px', color: '#78350f', marginTop: '4px' }}>
                                            {chat.lastMessage || 'Waiting for support...'}
                                        </div>
                                        <div style={{ fontSize: '12px', color: '#a16207', marginTop: '4px' }}>
                                            {formatDate(chat.startedAt)}
                                        </div>
                                    </div>
                                    <button
                                        onClick={() => acceptPendingChat(chat)}
                                        style={{
                                            width: '100%',
                                            padding: '8px',
                                            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
                                            color: 'white',
                                            border: 'none',
                                            borderRadius: '6px',
                                            cursor: 'pointer',
                                            fontWeight: '600',
                                            fontSize: '14px'
                                        }}
                                    >
                                        Accept Chat
                                    </button>
                                </div>
                            ))
                        )
                    )}
                </div>
            </div>

            {/* Chat Area */}
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                {selectedSession ? (
                    <>
                        {/* Chat Header */}
                        <div style={{ padding: '20px', background: 'white', borderBottom: '1px solid #e5e7eb' }}>
                            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: '600' }}>
                                Chat with {selectedSession.clientId || 'Client'}
                            </h3>
                            <div style={{ fontSize: '13px', color: '#6b7280', marginTop: '4px' }}>
                                Status: <span style={{
                                    color: connectionStatus === 'connected' ? '#22c55e' : connectionStatus === 'connecting' ? '#eab308' : '#ef4444',
                                    fontWeight: '600'
                                }}>
                                    {connectionStatus}
                                </span>
                                {' • '}
                                <span style={{
                                    color: selectedSession.status === 'active' ? '#22c55e' : '#eab308',
                                    fontWeight: '600'
                                }}>
                                    {selectedSession.status || 'active'}
                                </span>
                            </div>
                        </div>

                        {/* Messages */}
                        <div style={{ flex: 1, overflowY: 'auto', padding: '20px', background: '#f9fafb' }}>
                            {messages.map(msg => (
                                <div
                                    key={msg.messageId}
                                    style={{
                                        display: 'flex',
                                        justifyContent: msg.senderRole === 'admin' ? 'flex-end' : msg.senderRole === 'system' ? 'center' : 'flex-start',
                                        marginBottom: '16px'
                                    }}
                                >
                                    <div style={{ maxWidth: msg.senderRole === 'system' ? '100%' : '70%' }}>
                                        <div style={{
                                            padding: '10px 14px',
                                            borderRadius: '12px',
                                            background: msg.senderRole === 'admin'
                                                ? 'linear-gradient(to right, #9333ea, #6b21a8)'
                                                : msg.senderRole === 'system'
                                                    ? '#fef3c7'
                                                    : 'white',
                                            color: msg.senderRole === 'admin' ? 'white' : msg.senderRole === 'system' ? '#92400e' : '#1f2937',
                                            fontSize: '14px',
                                            border: msg.senderRole !== 'admin' ? '1px solid #e5e7eb' : 'none',
                                            fontStyle: msg.senderRole === 'system' ? 'italic' : 'normal',
                                            textAlign: msg.senderRole === 'system' ? 'center' : 'left'
                                        }}>
                                            {msg.message}
                                        </div>
                                        <div style={{
                                            fontSize: '12px',
                                            color: '#9ca3af',
                                            marginTop: '4px',
                                            textAlign: msg.senderRole === 'admin' ? 'right' : msg.senderRole === 'system' ? 'center' : 'left'
                                        }}>
                                            {formatTime(msg.timestamp)}
                                        </div>
                                    </div>
                                </div>
                            ))}
                            <div ref={messagesEndRef} />
                        </div>

                        {/* Input */}
                        <div style={{ padding: '20px', background: 'white', borderTop: '1px solid #e5e7eb' }}>
                            <div style={{ display: 'flex', gap: '12px' }}>
                                <input
                                    type="text"
                                    value={inputMessage}
                                    onChange={(e) => setInputMessage(e.target.value)}
                                    onKeyPress={(e) => e.key === 'Enter' && sendMessage()}
                                    placeholder="Type your message..."
                                    disabled={connectionStatus !== 'connected'}
                                    style={{
                                        flex: 1,
                                        padding: '12px 16px',
                                        border: '2px solid #e5e7eb',
                                        borderRadius: '24px',
                                        outline: 'none',
                                        fontSize: '14px',
                                        background: connectionStatus !== 'connected' ? '#f3f4f6' : 'white'
                                    }}
                                />
                                <button
                                    onClick={sendMessage}
                                    disabled={connectionStatus !== 'connected' || !inputMessage.trim()}
                                    style={{
                                        padding: '12px 24px',
                                        background: 'linear-gradient(to right, #9333ea, #6b21a8)',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '24px',
                                        cursor: 'pointer',
                                        fontWeight: '600',
                                        opacity: connectionStatus !== 'connected' || !inputMessage.trim() ? 0.5 : 1,
                                        transition: 'transform 0.2s'
                                    }}
                                    onMouseEnter={(e) => {
                                        if (connectionStatus === 'connected' && inputMessage.trim()) {
                                            e.currentTarget.style.transform = 'scale(1.05)';
                                        }
                                    }}
                                    onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                                >
                                    Send
                                </button>
                            </div>
                        </div>
                    </>
                ) : (
                    <div style={{
                        flex: 1,
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: '#9ca3af',
                        fontSize: '16px'
                    }}>
                        <div style={{ fontSize: '64px', marginBottom: '16px' }}>💬</div>
                        <div>Select a chat or start a new conversation</div>
                        {pendingChats.length > 0 && (
                            <div style={{ marginTop: '16px', fontSize: '14px', color: '#f59e0b' }}>
                                {pendingChats.length} pending {pendingChats.length === 1 ? 'request' : 'requests'} waiting
                            </div>
                        )}
                    </div>
                )}
            </div>

            {/* New Chat Modal */}
            {showNewChatModal && (
                <div style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    background: 'rgba(0, 0, 0, 0.5)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 1000
                }}>
                    <div style={{
                        background: 'white',
                        borderRadius: '12px',
                        padding: '24px',
                        width: '400px',
                        maxWidth: '90%'
                    }}>
                        <h3 style={{ margin: '0 0 20px 0' }}>Start New Chat</h3>
                        <input
                            type="text"
                            placeholder="Client ID"
                            value={newChatClientId}
                            onChange={(e) => setNewChatClientId(e.target.value)}
                            style={{
                                width: '100%',
                                padding: '10px',
                                border: '2px solid #e5e7eb',
                                borderRadius: '8px',
                                marginBottom: '12px',
                                outline: 'none',
                                boxSizing: 'border-box'
                            }}
                        />
                        <textarea
                            placeholder="Initial message (optional)"
                            value={newChatMessage}
                            onChange={(e) => setNewChatMessage(e.target.value)}
                            style={{
                                width: '100%',
                                padding: '10px',
                                border: '2px solid #e5e7eb',
                                borderRadius: '8px',
                                marginBottom: '16px',
                                outline: 'none',
                                minHeight: '80px',
                                resize: 'vertical',
                                boxSizing: 'border-box'
                            }}
                        />
                        <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => {
                                    setShowNewChatModal(false);
                                    setNewChatClientId('');
                                    setNewChatMessage('');
                                }}
                                style={{
                                    padding: '10px 20px',
                                    background: '#e5e7eb',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer',
                                    fontWeight: '600'
                                }}
                            >
                                Cancel
                            </button>
                            <button
                                onClick={startNewChat}
                                disabled={!newChatClientId.trim()}
                                style={{
                                    padding: '10px 20px',
                                    background: 'linear-gradient(to right, #9333ea, #6b21a8)',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer',
                                    fontWeight: '600',
                                    opacity: !newChatClientId.trim() ? 0.5 : 1
                                }}
                            >
                                Start Chat
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AdminChat;