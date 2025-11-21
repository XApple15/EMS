import axios from 'axios';

const defaultTimeout = 10000;
const defaultHeaders = {
    'Content-Type': 'application/json',
};

export const authClient = axios.create({
    baseURL: 'http://auth.docker.localhost',
    timeout: defaultTimeout,
    headers: defaultHeaders,
});

export const userClient = axios.create({
    baseURL: 'http://user.docker.localhost',
    timeout: defaultTimeout,
    headers: defaultHeaders,
});

export const deviceClient = axios.create({
    baseURL: 'http://device.docker.localhost',
    timeout: defaultTimeout,
    headers: defaultHeaders,
});

const attachAuthToken = (config) => {
    try {
        const token = localStorage.getItem('token');
        if (token) {
            config.headers = config.headers || {};
            config.headers.Authorization = `Bearer ${token}`;
        }
    } catch (e) {
    }
    return config;
};

const handleResponseError = (error) => {
    if (error?.response?.status === 401) {
        try {
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            localStorage.removeItem('roles');
            if (typeof window !== 'undefined') {
                window.location.href = '/login';
            }
        } catch (e) {
        }
    }
    return Promise.reject(error);
};

[authClient, userClient, deviceClient].forEach((client) => {
    client.interceptors.request.use(
        (config) => attachAuthToken(config),
        (error) => Promise.reject(error)
    );

    client.interceptors.response.use(
        (response) => response,
        (error) => handleResponseError(error)
    );
});

export default authClient;