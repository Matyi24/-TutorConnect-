"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const config_1 = __importDefault(require("../../../config"));
const { CLIENT_SETTINGS } = config_1.default;
const { GENERAL_ENDPOINT } = CLIENT_SETTINGS;
class EmailCampaignsApi {
    constructor(client) {
        this.client = client;
        // Token-scoped: the account is resolved from the API token server-side,
        // so the path takes no account id.
        this.emailCampaignsURL = `${GENERAL_ENDPOINT}/api/email_campaigns`;
    }
    /**
     * Lists the account's email campaigns, newest first. The result is wrapped in
     * a `{ data, pagination }` envelope; pagination is page-token based.
     */
    async getList(params) {
        const url = this.emailCampaignsURL;
        const query = {
            ...(params?.per_page !== undefined && { per_page: params.per_page }),
            ...(params?.search !== undefined && { search: params.search }),
            ...(params?.token !== undefined && { token: params.token }),
        };
        return this.client.get(url, { params: query });
    }
    /**
     * Create a new email campaign in the `draft` state. The campaign must
     * reference an existing sending domain via `domain_id` and include
     * a template `subject` within `template_attributes`.
     */
    async create(params) {
        const url = this.emailCampaignsURL;
        return this.client.post(url, params);
    }
    /**
     * Get a single email campaign by ID.
     */
    async get(id) {
        const url = `${this.emailCampaignsURL}/${id}`;
        return this.client.get(url);
    }
    /**
     * Update an existing `draft` email campaign. Only the provided attributes
     * are changed (PATCH semantics).
     */
    async update(id, params) {
        const url = `${this.emailCampaignsURL}/${id}`;
        return this.client.patch(url, params);
    }
    /**
     * Delete an email campaign by ID. Only a campaign in the `draft` state can be
     * deleted. Returns nothing (204 No Content).
     */
    async delete(id) {
        const url = `${this.emailCampaignsURL}/${id}`;
        return this.client.delete(url);
    }
    /**
     * Start sending a `draft` campaign immediately. Runs full sending validation;
     * on failure the request fails with `422` and the campaign stays a `draft`.
     */
    async start(id) {
        const url = `${this.emailCampaignsURL}/${id}/start`;
        return this.client.post(url);
    }
    /**
     * Schedule a `draft` campaign to start sending at a future time. After
     * scheduling, the time is reported back in
     * `current_state_metadata.scheduled_at`.
     */
    async schedule(id, params) {
        const url = `${this.emailCampaignsURL}/${id}/schedule`;
        return this.client.post(url, params);
    }
    /**
     * Cancel a `scheduled` campaign, removing the pending send job and returning
     * the campaign to the `draft` state.
     */
    async cancel(id) {
        const url = `${this.emailCampaignsURL}/${id}/cancel`;
        return this.client.post(url);
    }
    /**
     * Terminate a campaign that is currently sending (`started`, `queued`, or
     * `paused`), aborting the in-flight send.
     */
    async terminate(id) {
        const url = `${this.emailCampaignsURL}/${id}/terminate`;
        return this.client.post(url);
    }
    /**
     * Reset a `scheduled` campaign back to the `draft` state.
     */
    async reset(id) {
        const url = `${this.emailCampaignsURL}/${id}/reset`;
        return this.client.post(url);
    }
    /**
     * Get aggregated performance statistics for a single campaign. If the
     * campaign has never been started, all counts and rates are returned as `0`.
     * Use `start_date`/`end_date` (`YYYY-MM-DD`) to narrow the aggregation window.
     */
    async getStats(id, params) {
        const url = `${this.emailCampaignsURL}/${id}/stats`;
        const query = {
            ...(params?.start_date !== undefined && {
                start_date: params.start_date,
            }),
            ...(params?.end_date !== undefined && { end_date: params.end_date }),
        };
        return this.client.get(url, { params: query });
    }
}
exports.default = EmailCampaignsApi;
