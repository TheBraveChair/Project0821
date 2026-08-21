export class ApiClient {
    constructor(url) {
        this.baseUrl = url;
        this.token = localStorage.getItem('jwt_token') || null;
    }
    async _request(endpoint, data = null) {
        const headers = { 'Content-Type': 'application/json' };
        if (this.token) {
            headers['Authorization'] = `Bearer ${this.token}`;
        }
        const options = { method: 'POST', headers };
        if (data !== null) options.body = JSON.stringify(data);
        const response = await fetch(`${this.baseUrl}${endpoint}`, options);
        if (!response.ok) {
            const errText = await response.text();
            throw new Error(errText || `HTTP ${response.status}`);
        }
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    }
    async login(username, password) {
        const res = await this._request('/api/Auth/Login', { username, password });
        if (res && res.token) {
            this.token = res.token;
            localStorage.setItem('jwt_token', res.token);
        }
        return res;
    }
    logout() {
        this.token = null;
        localStorage.removeItem('jwt_token');
    }
    async register(username, password, role = 0) {
        return await this._request('/api/Auth/Register', { username, password, role });
    }
    async removeUser(id) {
        return await this._request('/api/Auth/RemoveUser', { id });
    }
    async modifyRole(id, role) {
        return await this._request('/api/Auth/ModifyRole', { id, role });
    }
    async myMap() {
        return await this._request('/api/Machine/MyMap');
    }
    async getUserMap(id) {
        return await this._request('/api/Machine/GetUserMap', { id });
    }
    async addMap(id, owner) {
        return await this._request('/api/Machine/AddMap', { id, owner });
    }
    async removeMap(id, owner) {
        return await this._request('/api/Machine/RemoveMap', { id, owner });
    }
    /**
     * @param {Array} fields - 格式: [{ name: "temp", valueType: 2, unit: "℃", description: "温度" }] 
     *                         (FieldType: String = 1, Decimal = 2)
     */
    async registerDeviceType(name, description, verifyExpression, fields) {
        return await this._request('/api/Machine/RegisterDeviceType', {
            name, description, verifyExpression, fields
        });
    }
    async getDeviceType(id) {
        return await this._request('/api/Machine/GetDeviceType', { id });
    }
    async removeDeviceType(id) {
        return await this._request('/api/Machine/RemoveDeviceType', { id });
    }
    async registerDevice(ownerId, deviceTypeId, deviceCode) {
        return await this._request('/api/Machine/RegisterDevice', {
            ownerId, deviceTypeId, deviceCode
        });
    }
    async getDevice(id) {
        return await this._request('/api/Machine/GetDevice', { id });
    }
    async removeDevice(id) {
        return await this._request('/api/Machine/RemoveDevice', { id });
    }
    async getMachines() {
        return await this._request('/api/Machine/GetMachines');
    }
    async getDevices(machineId) {
        var id = machineId;
        return await this._request('/api/Machine/GetDevices', { id });
    }
    async rangeValue(deviceId, seconds = 60, start = null) {
        return await this._request('/api/Machine/RangeValue', { device: deviceId, start, length: seconds });
    }
    async allDeviceType() {
        return await this._request('/api/Machine/AllDeviceType');
    }
    async queryAlertDevices(machineId) {
        return await this._request('/api/Machine/QueryAlertDevices', { id: machineId });
    }
    async confirmAlert(deviceId) {
        return await this._request('/api/Machine/ConfirmAlert', { id: deviceId });
    }
}
