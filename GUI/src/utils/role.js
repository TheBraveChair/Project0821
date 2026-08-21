import { ApiClient } from './api'
class ViewerClient {
    constructor(apiClient) {
        this.api = apiClient;
    }
    async login(username, password) {
        return await this.api.login(username, password);
    }
    logout() {
        this.api.logout();
    }
    async myMap() {
        return await this.api.myMap();
    }
    async getMachines() {
        return await this.api.getMachines();
    }
    async getDeviceType(id) {
        return await this.api.getDeviceType(id);
    }
    async getDevice(id) {
        return await this.api.getDevice(id);
    }
    async getDevices(machineId) {
        return await this.api.getDevices(machineId);
    }
    async rangeValue(deviceId, seconds, start) {
        return await this.api.rangeValue(deviceId, seconds, start);
    }
}
class OperatorClient extends ViewerClient {
    async registerDevice(ownerId, deviceTypeId, deviceCode) {
        return await this.api.registerDevice(ownerId, deviceTypeId, deviceCode);
    }
    async removeDevice(id) {
        return await this.api.removeDevice(id);
    }
    async allDeviceType() {
        return await this.api.allDeviceType();
    }
    async confirmAlert(id) {
        return await this.api.confirmAlert(id);
    }
    async queryAlertDevices(machineId) {
        return await this.api.queryAlertDevices(machineId);
    }
}
class AdminClient extends OperatorClient {
    async registerUser(username, password, role = 0) {
        return await this.api.register(username, password, role);
    }
    async removeUser(id) {
        return await this.api.removeUser(id);
    }
    async modifyRole(id, role) {
        return await this.api.modifyRole(id, role);
    }
    async getUserMap(id) {
        return await this.api.getUserMap(id);
    }
    async addMap(id, owner) {
        return await this.api.addMap(id, owner);
    }
    async removeMap(id, owner) {
        return await this.api.removeMap(id, owner);
    }
    async registerDeviceType(name, description, verifyExpression, fields) {
        return await this.api.registerDeviceType(name, description, verifyExpression, fields);
    }
    async removeDeviceType(id) {
        return await this.api.removeDeviceType(id);
    }
}
class RoleType {
    static Viewer = 0;
    static Operator = 1;
    static Admin = 2;
}
export async function login(name, password) {
    var client = new ApiClient('');
    try {
        var resp = await client.login(name, password);
        switch (resp.role) {
            case RoleType.Viewer:
                return new ViewerClient(client);
            case RoleType.Operator:
                return new OperatorClient(client);
            case RoleType.Admin:
                return new AdminClient(client);
        }
        console.log(resp);
    } catch (e) {
        console.error(e);
        return null;
    }
}