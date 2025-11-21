import authClient, { userClient, deviceClient } from './axios';


export const userService = {
    getAllUsers: async () => {
        const resp = await userClient.get('/api/user');
        return resp.data;
    },

    getUserById: async (id) => {
        const resp = await userClient.get(`/api/user/${id}`);
        return resp.data;
    },

    getUserByAuthId: async (authId) => {
        const resp = await userClient.get('/api/user/by-auth', { params: { authId } });
        return resp.data;
    },

    createUserProfile: async (dto) => {
        const resp = await userClient.post('/api/user', dto);
        return resp.data;
    },

    updateUser: async (id, dto) => {
        const resp = await userClient.put(`/api/user/${id}`, dto);
        return resp.data;
    },

    deleteUserProfile: async (id) => {
        const resp = await userClient.delete(`/api/user/${id}`);
        return resp.data;
    },

    createUser: async ({ name, email, password, roles = ['Client'], address = '' }) => {
        //  Create in Auth service
        const authResp = await authClient.post('/auth/register', { email, password, roles });
        const authData = authResp.data;
        const createdAuthId =
            authData?.id ??
            authData?.user?.id ??
            authData?.authId ??
            authData?.userId ??
            null;

        if (!createdAuthId) {
            throw new Error('Auth service did not return created id');
        }

        // Create profile in User service
        const profileDto = { AuthId: createdAuthId, Username: name, Address: address || '' };
        const userResp = await userClient.post('/api/user', profileDto);

        return { auth: authData, user: userResp.data };
    },

    deleteUser: async (userId) => {
        // Lookup profile for AuthId
        const profile = await userClient.get(`/api/user/${userId}`).then(r => r.data).catch(() => null);
        if (!profile) {
            throw new Error('User profile not found');
        }

        const authId =
            profile.authId ??
            profile.AuthId ??
            profile.authID ??
            profile.AuthID ??
            profile.authIdString ??
            null;

        // Delete profile
        await userClient.delete(`/api/user/${userId}`);

        // Delete auth identity
        if (authId) {
            try {
                await authClient.delete(`/auth/${authId}`);
            } catch (err) {
                console.warn('Failed to delete auth user, profile already removed', err);
            }
        }
        return true;
    }
};

export const deviceService = {
    getAllDevices: async () => {
        const resp = await deviceClient.get('/api/device');
        return resp.data;
    },

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
    },

  
    assignDeviceToUser: async (deviceId, userId) => {
        const current = await deviceClient.get(`/api/device/${deviceId}`).then(r => r.data);
        if (!current) throw new Error('Device not found');

        const dto = {
            userId: userId || null,
            name: current.name,
            consumption: String(current.consumption ?? '')
        };
        const resp = await deviceClient.put(`/api/device/${deviceId}`, dto);
        return resp.data;
    }
};

export default userService;