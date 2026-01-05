import React, { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';

const ChatWidget = () => {
    const [isOpen, setIsOpen] = useState(false);
    const [showNotifications, setShowNotifications] = useState(false);
    const [chatMode, setChatMode] = useState('bot'); // 'bot' or 'admin'
    const [messages, setMessages] = useState([
        { id: 1, text: 'Hello! How can I help you today?', type: 'agent', timestamp: new Date() }
    ]);
    const [notifications, setNotifications] = useState([]);
    const [inputMessage, setInputMessage] = useState('');
    const [connectionStatus, setConnectionStatus] = useState('connecting');
    const [isTyping, setIsTyping] = useState(false);
    const [unreadCount, setUnreadCount] = useState(0);
    const [unreadNotifications, setUnreadNotifications] = useState(0);
    const [userId, setUserId] = useState(null);
    const [adminChatSession, setAdminChatSession] = useState(null);
    const [adminChatStatus, setAdminChatStatus] = useState('none'); // 'none', 'pending', 'active'

    const messagesEndRef = useRef(null);
    const notificationsEndRef = useRef(null);
    const connectionRef = useRef(null);

    // Get actual database userId on component mount
    useEffect(() => {
        const loadUserId = async () => {
            try {
                const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
                if (storedUser && (storedUser.id || storedUser.Id)) {
                    const authId = storedUser.id ?? storedUser.Id;

                    // Fetch the actual user data to get the database user ID
                    const { userService } = await import('../api/api-service');
                    const userData = await userService.getUserByAuthId(authId);

                    if (userData && (userData.id || userData.Id)) {
                        const dbUserId = userData.id ?? userData.Id;
                        setUserId(dbUserId);
                        console.log('Database User ID loaded for notifications:', dbUserId);
                    } else {
                        console.warn('Could not load database user ID, falling back to auth ID');
                        setUserId(authId);
                    }
                }
            } catch (err) {
                console.error('Error loading user ID:', err);
                // Fallback to auth ID if API call fails
                try {
                    const storedUser = JSON.parse(localStorage.getItem('user') || 'null');
                    if (storedUser && (storedUser.id || storedUser.Id)) {
                        const authId = storedUser.id ?? storedUser.Id;
                        setUserId(authId);
                        console.log('Fallback: Using auth ID:', authId);
                    }
                } catch (e) {
                    console.error('Failed to load any user ID');
                }
            }
        };

        loadUserId();
    }, []);

    // Check for active admin chat session
    useEffect(() => {
        if (!userId) return;

        const checkActiveChat = async () => {
            try {
                const response = await axios.get(`http://customersupport.docker.localhost/api/AdminChat/active/${userId}`);
                if (response.data.success && response.data.session) {
                    setAdminChatSession(response.data.session);
                    // Set status based on whether admin is assigned
                    if (response.data.session.adminId) {
                        setAdminChatStatus('active');
                    } else {
                        setAdminChatStatus('pending');
                    }
                    setChatMode('admin');

                    // Load messages for this session
                    await loadAdminChatMessages(response.data.session.chatRoomId);
                }
            } catch (error) {
                console.error('Error checking active chat:', error);
            }
        };

        checkActiveChat();
    }, [userId]);

    const loadAdminChatMessages = async (chatRoomId) => {
        try {
            const response = await axios.get(`http://customersupport.docker.localhost/api/AdminChat/messages/${chatRoomId}`);
            if (response.data.success) {
                const adminMessages = response.data.messages.map(msg => ({
                    id: msg.messageId,
                    text: msg.message,
                    type: msg.senderRole === 'user' ? 'user' : msg.senderRole === 'system' ? 'system' : 'agent',
                    timestamp: new Date(msg.timestamp),
                    senderRole: msg.senderRole
                }));
                setMessages(adminMessages);
            }
        } catch (error) {
            console.error('Error loading admin chat messages:', error);
        }
    };

    const initiateAdminChat = async () => {
        if (!userId) {
            console.error('No user ID available');
            return;
        }

        try {
            const response = await axios.post('http://customersupport.docker.localhost/api/AdminChat/initiate', {
                userId: userId,
                initialMessage: 'I need help from an admin'
            });

            if (response.data.success) {
                setAdminChatSession(response.data.session);
                setAdminChatStatus('pending');
                setChatMode('admin');
                setMessages([
                    {
                        id: Date.now(),
                        text: 'Connecting you with an admin. Please wait...',
                        type: 'system',
                        timestamp: new Date()
                    }
                ]);

                // Join the chat room
                if (connectionRef.current && connectionRef.current.state === signalR.HubConnectionState.Connected) {
                    await connectionRef.current.invoke('JoinChatRoom', response.data.session.chatRoomId);
                }
            }
        } catch (error) {
            console.error('Error initiating admin chat:', error);
            setMessages(prev => [...prev, {
                id: Date.now(),
                text: 'Sorry, we could not connect you to an admin. Please try again later.',
                type: 'system',
                timestamp: new Date()
            }]);
        }
    };

    const switchToAdminChat = () => {
        setChatMode('admin');
        initiateAdminChat();
    };

    const switchToBotChat = () => {
        setChatMode('bot');
        setAdminChatStatus('none');
        setAdminChatSession(null);
        setMessages([
            { id: 1, text: 'Hello! How can I help you today?', type: 'agent', timestamp: new Date() }
        ]);
    };

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    };

    const scrollNotificationsToBottom = () => {
        notificationsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    };

    useEffect(() => {
        scrollToBottom();
    }, [messages, isTyping]);

    useEffect(() => {
        scrollNotificationsToBottom();
    }, [notifications]);

    useEffect(() => {
        // Only connect if we have a userId
        if (!userId) {
            console.log('Waiting for userId to establish SignalR connection...');
            return;
        }

        // Create SignalR connection
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('http://websocket.docker.localhost/chathub', {
                skipNegotiation: false,
                withCredentials: true,
                transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Debug)
            .build();

        connectionRef.current = connection;

        // Connection established
        connection.on('Connected', (connectionId) => {
            console.log('Connected with Connection ID:', connectionId);
            console.log('Using User ID for notifications:', userId);
            setConnectionStatus('connected');

            // Register userId with the hub
            connection.invoke('RegisterUser', userId, 'user')
                .then(() => {
                    console.log('User ID registered successfully');

                    // Join admin chat room if we have an active session
                    if (adminChatSession && adminChatSession.chatRoomId) {
                        connection.invoke('JoinChatRoom', adminChatSession.chatRoomId)
                            .then(() => console.log('Joined admin chat room:', adminChatSession.chatRoomId))
                            .catch(err => console.error('Error joining admin chat room:', err));
                    }
                })
                .catch(err => console.error('Error registering user ID:', err));
        });

        // Registered confirmation
        connection.on('Registered', (data) => {
            console.log('Registration confirmed:', data);
        });

        // Joined chat room confirmation
        connection.on('JoinedChatRoom', (chatRoomId) => {
            console.log('Joined chat room:', chatRoomId);
        });

        // Message sent confirmation
        connection.on('MessageSent', (message) => {
            console.log('Message sent:', message);
        });

        // Receive admin chat message
        connection.on('ReceiveAdminChatMessage', (message) => {
            console.log('Received admin chat message:', message);

            setMessages(prev => {
                // Avoid duplicates
                if (prev.some(m => m.id === message.messageId)) {
                    return prev;
                }
                return [...prev, {
                    id: message.messageId,
                    text: message.message,
                    type: message.senderRole === 'user' ? 'user' : message.senderRole === 'system' ? 'system' : 'agent',
                    timestamp: new Date(message.timestamp),
                    senderRole: message.senderRole
                }];
            });

            // Update status if admin joined - check for system message with specific content
            if (message.senderRole === 'system' &&
                (message.message.toLowerCase().includes('admin') &&
                    message.message.toLowerCase().includes('joined'))) {
                setAdminChatStatus('active');
            }

            // Increment unread count if chat is closed
            if (!isOpen && message.senderRole !== 'user') {
                setUnreadCount(prev => prev + 1);
            }
        });

        // Receive answer from support (bot)
        connection.on('ReceiveAnswer', (message, timestamp) => {
            console.log('Received answer:', message);
            setIsTyping(false);
            setMessages(prev => [...prev, {
                id: Date.now(),
                text: message,
                type: 'agent',
                timestamp: new Date(timestamp)
            }]);

            // Increment unread count if chat is closed
            if (!isOpen) {
                setUnreadCount(prev => prev + 1);
            }
        });

        // Receive notification from backend
        connection.on('ReceiveNotification', (notification) => {
            console.log('Received notification:', notification);

            // Show browser notification if supported
            if ('Notification' in window && Notification.permission === 'granted') {
                new Notification(notification.title, {
                    body: notification.message,
                    icon: '/notification-icon.png'
                });
            }

            // Add to notifications list
            const newNotification = {
                id: Date.now(),
                title: notification.title,
                message: notification.message,
                type: notification.type,
                timestamp: new Date(notification.timestamp),
                read: showNotifications // Mark as read if notifications panel is open
            };

            setNotifications(prev => [newNotification, ...prev]);

            // Increment unread notifications if panel is closed
            if (!showNotifications) {
                setUnreadNotifications(prev => prev + 1);
            }
        });

        // Handle reconnecting
        connection.onreconnecting(() => {
            setConnectionStatus('connecting');
        });

        // Handle reconnected
        connection.onreconnected(() => {
            setConnectionStatus('connected');
        });

        // Handle connection closed
        connection.onclose(() => {
            setConnectionStatus('disconnected');
        });

        // Start connection
        const startConnection = async () => {
            try {
                await connection.start();
                console.log('SignalR Connected');

                // Request notification permission
                if ('Notification' in window && Notification.permission === 'default') {
                    Notification.requestPermission();
                }
            } catch (err) {
                console.error('Error connecting:', err);
                setConnectionStatus('disconnected');
                setTimeout(startConnection, 5000);
            }
        };

        startConnection();

        // Cleanup
        return () => {
            connection.stop();
        };
    }, [isOpen, userId, showNotifications]);

    const toggleChat = () => {
        setIsOpen(!isOpen);
        setShowNotifications(false);
        if (!isOpen) {
            setUnreadCount(0);
        }
    };

    const toggleNotifications = () => {
        setShowNotifications(!showNotifications);
        setIsOpen(false);
        if (!showNotifications) {
            setUnreadNotifications(0);
            // Mark all as read
            setNotifications(prev => prev.map(n => ({ ...n, read: true })));
        }
    };

    const clearNotifications = () => {
        setNotifications([]);
        setUnreadNotifications(0);
    };

    const sendMessage = async () => {
        const text = inputMessage.trim();
        if (!text || connectionRef.current?.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            if (chatMode === 'admin' && adminChatSession) {
                // Send admin chat message via API
                const response = await axios.post('http://customersupport.docker.localhost/api/AdminChat/send', {
                    chatRoomId: adminChatSession.chatRoomId,
                    senderId: userId,
                    senderRole: 'user',
                    message: text
                });

                if (response.data.success) {
                    // Message will be received via SignalR, don't add it here
                    setInputMessage('');
                }
            } else {
                // Send question to bot
                await connectionRef.current.invoke('SendQuestion', text);

                setMessages(prev => [...prev, {
                    id: Date.now(),
                    text: text,
                    type: 'user',
                    timestamp: new Date()
                }]);

                setInputMessage('');
                setIsTyping(true);
            }
        } catch (err) {
            console.error('Error sending message:', err);
        }
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    };

    const formatTime = (date) => {
        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    const formatNotificationTime = (date) => {
        const now = new Date();
        const diff = Math.floor((now - date) / 1000); // seconds

        if (diff < 60) return 'Just now';
        if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
        if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    };

    const getStatusColor = () => {
        switch (connectionStatus) {
            case 'connected': return '#22c55e';
            case 'connecting': return '#eab308';
            case 'disconnected': return '#ef4444';
            default: return '#6b7280';
        }
    };

    const getNotificationColor = (type) => {
        switch (type) {
            case 'success': return { bg: '#dcfce7', border: '#86efac', text: '#166534', icon: '✓' };
            case 'warning': return { bg: '#fef3c7', border: '#fcd34d', text: '#92400e', icon: '⚠' };
            case 'error': return { bg: '#fee2e2', border: '#fca5a5', text: '#991b1b', icon: '✕' };
            case 'info':
            default: return { bg: '#dbeafe', border: '#93c5fd', text: '#1e40af', icon: 'ℹ' };
        }
    };

    const styles = {
        chatButton: {
            position: 'fixed',
            bottom: '24px',
            right: '24px',
            width: '64px',
            height: '64px',
            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
            color: 'white',
            borderRadius: '50%',
            border: 'none',
            boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 50,
            transition: 'transform 0.3s',
        },
        notificationButton: {
            position: 'fixed',
            bottom: '24px',
            right: '104px',
            width: '64px',
            height: '64px',
            background: 'linear-gradient(to right, #3b82f6, #2563eb)',
            color: 'white',
            borderRadius: '50%',
            border: 'none',
            boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 50,
            transition: 'transform 0.3s',
        },
        unreadBadge: {
            position: 'absolute',
            top: '-4px',
            right: '-4px',
            minWidth: '24px',
            height: '24px',
            background: '#ef4444',
            color: 'white',
            fontSize: '12px',
            borderRadius: '12px',
            padding: '0 6px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 'bold',
        },
        chatWindow: {
            position: 'fixed',
            bottom: '24px',
            right: '24px',
            width: '384px',
            height: '600px',
            background: 'white',
            borderRadius: '16px',
            boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            zIndex: 50,
            animation: 'slideUp 0.3s ease-out',
        },
        notificationsPanel: {
            position: 'fixed',
            bottom: '24px',
            right: '24px',
            width: '384px',
            height: '600px',
            background: 'white',
            borderRadius: '16px',
            boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            zIndex: 50,
            animation: 'slideUp 0.3s ease-out',
        },
        header: {
            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
            color: 'white',
            padding: '16px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
        },
        notificationHeader: {
            background: 'linear-gradient(to right, #3b82f6, #2563eb)',
            color: 'white',
            padding: '16px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
        },
        headerLeft: {
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
        },
        avatarContainer: {
            position: 'relative',
        },
        avatar: {
            width: '40px',
            height: '40px',
            background: 'white',
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: '24px',
        },
        statusIndicator: {
            position: 'absolute',
            bottom: '0',
            right: '0',
            width: '12px',
            height: '12px',
            background: getStatusColor(),
            borderRadius: '50%',
            border: '2px solid white',
        },
        headerTitle: {
            fontWeight: 'bold',
            margin: 0,
            fontSize: '16px',
        },
        headerSubtitle: {
            fontSize: '12px',
            opacity: 0.9,
            margin: 0,
        },
        headerActions: {
            display: 'flex',
            gap: '8px',
        },
        headerButton: {
            width: '32px',
            height: '32px',
            background: 'rgba(255, 255, 255, 0.2)',
            border: 'none',
            borderRadius: '50%',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: 'white',
            transition: 'background 0.2s',
            fontSize: '12px',
        },
        closeButton: {
            width: '32px',
            height: '32px',
            background: 'rgba(255, 255, 255, 0.2)',
            border: 'none',
            borderRadius: '50%',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: 'white',
            transition: 'background 0.2s',
        },
        messagesContainer: {
            flex: 1,
            overflowY: 'auto',
            padding: '16px',
            background: '#f9fafb',
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
        },
        notificationsContainer: {
            flex: 1,
            overflowY: 'auto',
            background: '#f9fafb',
        },
        emptyNotifications: {
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            height: '100%',
            padding: '32px',
            textAlign: 'center',
            color: '#6b7280',
        },
        notificationItem: {
            padding: '16px',
            borderBottom: '1px solid #e5e7eb',
            cursor: 'pointer',
            transition: 'background 0.2s',
        },
        notificationItemUnread: {
            background: '#f0f9ff',
        },
        notificationIcon: {
            width: '36px',
            height: '36px',
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: '18px',
            fontWeight: 'bold',
        },
        messageWrapper: {
            display: 'flex',
            animation: 'fadeIn 0.3s ease-in',
        },
        messageWrapperUser: {
            justifyContent: 'flex-end',
        },
        messageWrapperAgent: {
            justifyContent: 'flex-start',
        },
        messageContent: {
            maxWidth: '75%',
        },
        messageBubbleUser: {
            padding: '8px 16px',
            borderRadius: '16px',
            borderBottomRightRadius: '4px',
            fontSize: '14px',
            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
            color: 'white',
        },
        messageBubbleAgent: {
            padding: '8px 16px',
            borderRadius: '16px',
            borderBottomLeftRadius: '4px',
            fontSize: '14px',
            background: 'white',
            color: '#1f2937',
            border: '1px solid #e5e7eb',
        },
        messageTime: {
            fontSize: '12px',
            color: '#6b7280',
            marginTop: '4px',
            paddingLeft: '4px',
            paddingRight: '4px',
        },
        messageTimeRight: {
            textAlign: 'right',
        },
        messageTimeLeft: {
            textAlign: 'left',
        },
        typingIndicator: {
            display: 'flex',
            justifyContent: 'flex-start',
            animation: 'fadeIn 0.3s ease-in',
        },
        typingBubble: {
            background: 'white',
            border: '1px solid #e5e7eb',
            borderRadius: '16px',
            borderBottomLeftRadius: '4px',
            padding: '12px 16px',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '4px',
        },
        typingDot: {
            width: '8px',
            height: '8px',
            background: '#9ca3af',
            borderRadius: '50%',
            animation: 'bounce 1.4s infinite',
        },
        inputContainer: {
            padding: '16px',
            background: 'white',
            borderTop: '1px solid #e5e7eb',
        },
        inputWrapper: {
            display: 'flex',
            gap: '8px',
        },
        input: {
            flex: 1,
            padding: '8px 16px',
            border: '2px solid #e5e7eb',
            borderRadius: '24px',
            outline: 'none',
            fontSize: '14px',
            transition: 'border-color 0.2s',
        },
        inputDisabled: {
            background: '#f3f4f6',
            cursor: 'not-allowed',
        },
        sendButton: {
            padding: '8px 16px',
            background: 'linear-gradient(to right, #9333ea, #6b21a8)',
            color: 'white',
            border: 'none',
            borderRadius: '24px',
            fontWeight: '600',
            cursor: 'pointer',
            transition: 'transform 0.2s',
        },
        sendButtonDisabled: {
            opacity: 0.5,
            cursor: 'not-allowed',
        },
    };

    return (
        <>
            {/* Notification Button */}
            {!showNotifications && !isOpen && (
                <button
                    onClick={toggleNotifications}
                    style={styles.notificationButton}
                    onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.1)'}
                    onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                >
                    <svg style={{ width: '28px', height: '28px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                    </svg>
                    {unreadNotifications > 0 && (
                        <span style={styles.unreadBadge}>
                            {unreadNotifications > 99 ? '99+' : unreadNotifications}
                        </span>
                    )}
                </button>
            )}

            {/* Chat Button */}
            {!isOpen && !showNotifications && (
                <button
                    onClick={toggleChat}
                    style={styles.chatButton}
                    onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.1)'}
                    onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                >
                    <svg style={{ width: '32px', height: '32px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                    </svg>
                    {unreadCount > 0 && (
                        <span style={styles.unreadBadge}>
                            {unreadCount > 9 ? '9+' : unreadCount}
                        </span>
                    )}
                </button>
            )}

            {/* Notifications Panel */}
            {showNotifications && (
                <div style={styles.notificationsPanel}>
                    <div style={styles.notificationHeader}>
                        <div style={styles.headerLeft}>
                            <div style={styles.avatar}>
                                <span>🔔</span>
                            </div>
                            <div>
                                <h3 style={styles.headerTitle}>Notifications</h3>
                                <p style={styles.headerSubtitle}>
                                    {notifications.length} {notifications.length === 1 ? 'notification' : 'notifications'}
                                </p>
                            </div>
                        </div>
                        <div style={styles.headerActions}>
                            {notifications.length > 0 && (
                                <button
                                    onClick={clearNotifications}
                                    style={styles.headerButton}
                                    onMouseEnter={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.3)'}
                                    onMouseLeave={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)'}
                                    title="Clear all"
                                >
                                    🗑️
                                </button>
                            )}
                            <button
                                onClick={toggleNotifications}
                                style={styles.closeButton}
                                onMouseEnter={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.3)'}
                                onMouseLeave={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)'}
                            >
                                <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>
                    </div>

                    <div style={styles.notificationsContainer}>
                        {notifications.length === 0 ? (
                            <div style={styles.emptyNotifications}>
                                <div style={{ fontSize: '48px', marginBottom: '16px' }}>🔕</div>
                                <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', color: '#374151' }}>No notifications</h3>
                                <p style={{ margin: 0, fontSize: '14px' }}>You're all caught up!</p>
                            </div>
                        ) : (
                            notifications.map((notification) => {
                                const colors = getNotificationColor(notification.type);
                                return (
                                    <div
                                        key={notification.id}
                                        style={{
                                            ...styles.notificationItem,
                                            ...(!notification.read ? styles.notificationItemUnread : {})
                                        }}
                                        onMouseEnter={(e) => e.currentTarget.style.background = '#f3f4f6'}
                                        onMouseLeave={(e) => e.currentTarget.style.background = notification.read ? 'white' : '#f0f9ff'}
                                    >
                                        <div style={{ display: 'flex', gap: '12px' }}>
                                            <div style={{
                                                ...styles.notificationIcon,
                                                background: colors.bg,
                                                color: colors.text,
                                                border: `2px solid ${colors.border}`
                                            }}>
                                                {colors.icon}
                                            </div>
                                            <div style={{ flex: 1 }}>
                                                <h4 style={{ margin: '0 0 4px 0', fontSize: '14px', fontWeight: '600', color: '#1f2937' }}>
                                                    {notification.title}
                                                </h4>
                                                <p style={{ margin: '0 0 8px 0', fontSize: '13px', color: '#6b7280', lineHeight: '1.4' }}>
                                                    {notification.message}
                                                </p>
                                                <span style={{ fontSize: '12px', color: '#9ca3af' }}>
                                                    {formatNotificationTime(notification.timestamp)}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                );
                            })
                        )}
                        <div ref={notificationsEndRef} />
                    </div>
                </div>
            )}

            {/* Chat Window */}
            {isOpen && (
                <div style={styles.chatWindow}>
                    <div style={styles.header}>
                        <div style={styles.headerLeft}>
                            <div style={styles.avatarContainer}>
                                <div style={styles.avatar}>
                                    <span>{chatMode === 'admin' ? '👨‍💼' : '🤖'}</span>
                                </div>
                                <div style={styles.statusIndicator}></div>
                            </div>
                            <div>
                                <h3 style={styles.headerTitle}>
                                    {chatMode === 'admin' ? 'Admin Support' : 'Support Chat'}
                                </h3>
                                <p style={styles.headerSubtitle}>
                                    {chatMode === 'admin'
                                        ? (adminChatStatus === 'pending' ? 'Waiting for admin...' : 'Connected with admin')
                                        : (connectionStatus === 'connected' ? 'Online' : 'Connecting...')
                                    }
                                </p>
                            </div>
                        </div>
                        <button
                            onClick={toggleChat}
                            style={styles.closeButton}
                            onMouseEnter={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.3)'}
                            onMouseLeave={(e) => e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)'}
                        >
                            <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>

                    {/* Chat mode switcher */}
                    {chatMode === 'bot' && (
                        <div style={{
                            padding: '12px 16px',
                            borderBottom: '1px solid #e5e7eb',
                            background: '#f9fafb',
                            display: 'flex',
                            justifyContent: 'center'
                        }}>
                            <button
                                onClick={switchToAdminChat}
                                style={{
                                    padding: '8px 16px',
                                    background: 'linear-gradient(to right, #3b82f6, #2563eb)',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '20px',
                                    cursor: 'pointer',
                                    fontWeight: '600',
                                    fontSize: '13px',
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    transition: 'transform 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.05)'}
                                onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                            >
                                <span>👨‍💼</span>
                                <span>Chat with Admin</span>
                            </button>
                        </div>
                    )}

                    {chatMode === 'admin' && adminChatStatus !== 'none' && (
                        <div style={{
                            padding: '12px 16px',
                            borderBottom: '1px solid #e5e7eb',
                            background: adminChatStatus === 'pending' ? '#fef3c7' : '#dcfce7',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center'
                        }}>
                            <span style={{
                                fontSize: '13px',
                                color: adminChatStatus === 'pending' ? '#92400e' : '#166534',
                                fontWeight: '600'
                            }}>
                                {adminChatStatus === 'pending' ? '⏳ Waiting for admin to join...' : '✓ Admin is here'}
                            </span>
                            <button
                                onClick={switchToBotChat}
                                style={{
                                    padding: '6px 12px',
                                    background: 'rgba(0,0,0,0.1)',
                                    color: adminChatStatus === 'pending' ? '#92400e' : '#166534',
                                    border: 'none',
                                    borderRadius: '12px',
                                    cursor: 'pointer',
                                    fontSize: '11px',
                                    fontWeight: '600'
                                }}
                            >
                                Switch to Bot
                            </button>
                        </div>
                    )}

                    <div style={styles.messagesContainer}>
                        {messages.map((message) => (
                            <div
                                key={message.id}
                                style={{
                                    ...styles.messageWrapper,
                                    ...(message.type === 'system'
                                        ? { justifyContent: 'center' }
                                        : message.type === 'user'
                                            ? styles.messageWrapperUser
                                            : styles.messageWrapperAgent
                                    )
                                }}
                            >
                                <div style={message.type === 'system' ? { maxWidth: '90%' } : styles.messageContent}>
                                    <div style={
                                        message.type === 'system'
                                            ? {
                                                padding: '8px 16px',
                                                borderRadius: '12px',
                                                background: '#fef3c7',
                                                color: '#92400e',
                                                fontSize: '13px',
                                                fontStyle: 'italic',
                                                textAlign: 'center',
                                                border: '1px solid #fcd34d'
                                            }
                                            : message.type === 'user'
                                                ? styles.messageBubbleUser
                                                : styles.messageBubbleAgent
                                    }>
                                        {message.text}
                                    </div>
                                    <div style={{
                                        ...styles.messageTime,
                                        ...(message.type === 'system'
                                            ? { textAlign: 'center' }
                                            : message.type === 'user'
                                                ? styles.messageTimeRight
                                                : styles.messageTimeLeft
                                        )
                                    }}>
                                        {formatTime(message.timestamp)}
                                    </div>
                                </div>
                            </div>
                        ))}

                        {isTyping && (
                            <div style={styles.typingIndicator}>
                                <div style={styles.typingBubble}>
                                    <div style={{ ...styles.typingDot, animationDelay: '0ms' }}></div>
                                    <div style={{ ...styles.typingDot, animationDelay: '150ms' }}></div>
                                    <div style={{ ...styles.typingDot, animationDelay: '300ms' }}></div>
                                </div>
                            </div>
                        )}

                        <div ref={messagesEndRef} />
                    </div>

                    <div style={styles.inputContainer}>
                        <div style={styles.inputWrapper}>
                            <input
                                type="text"
                                value={inputMessage}
                                onChange={(e) => setInputMessage(e.target.value)}
                                onKeyPress={handleKeyPress}
                                placeholder="Type your message..."
                                disabled={connectionStatus !== 'connected'}
                                style={{
                                    ...styles.input,
                                    ...(connectionStatus !== 'connected' ? styles.inputDisabled : {})
                                }}
                                onFocus={(e) => e.currentTarget.style.borderColor = '#9333ea'}
                                onBlur={(e) => e.currentTarget.style.borderColor = '#e5e7eb'}
                            />
                            <button
                                onClick={sendMessage}
                                disabled={connectionStatus !== 'connected' || !inputMessage.trim()}
                                style={{
                                    ...styles.sendButton,
                                    ...(connectionStatus !== 'connected' || !inputMessage.trim() ? styles.sendButtonDisabled : {})
                                }}
                                onMouseEnter={(e) => {
                                    if (connectionStatus === 'connected' && inputMessage.trim()) {
                                        e.currentTarget.style.transform = 'scale(1.05)';
                                    }
                                }}
                                onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                            >
                                <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                                </svg>
                            </button>
                        </div>
                    </div>
                </div>
            )}

            <style>{`
                @keyframes fadeIn {
                    from {
                        opacity: 0;
                        transform: translateY(10px);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0);
                    }
                }

                @keyframes slideUp {
                    from {
                        opacity: 0;
                        transform: translateY(20px) scale(0.95);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0) scale(1);
                    }
                }

                @keyframes bounce {
                    0%, 60%, 100% {
                        transform: translateY(0);
                    }
                    30% {
                        transform: translateY(-10px);
                    }
                }
            `}</style>
        </>
    );
};

export default ChatWidget;