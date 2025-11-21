import { deviceClient } from '../api/axios';

export const deviceService = {
    getDevicesByUserId: async (userId) => {
        const resp = await deviceClient.get(`/api/device/user/${userId}`);
        return resp.data;
    },
    getDeviceById: async (deviceId) => {
        const resp = await deviceClient.get(`/api/device/${deviceId}`);
        return resp.data;
    },
    createDevice: async (dto) => {
        const resp = await deviceClient.post('/api/device', dto);
        return resp.data;
    },
    updateDevice: async (id, dto) => {
        const resp = await deviceClient.put(`/api/device/${id}`, dto);
        return resp.data;
    },
    deleteDevice: async (id) => {
        await deviceClient.delete(`/api/device/${id}`);
    }
};