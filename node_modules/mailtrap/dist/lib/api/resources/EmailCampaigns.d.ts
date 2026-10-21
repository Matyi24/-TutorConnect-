import { AxiosInstance } from "axios";
import { CreateEmailCampaignParams, CreateEmailCampaignResponse, EmailCampaignActionResponse, GetEmailCampaignResponse, GetEmailCampaignStatsParams, GetEmailCampaignStatsResponse, ListEmailCampaignsParams, ListEmailCampaignsResponse, ScheduleEmailCampaignParams, UpdateEmailCampaignParams, UpdateEmailCampaignResponse } from "../../../types/api/email-campaigns";
export default class EmailCampaignsApi {
    private client;
    private emailCampaignsURL;
    constructor(client: AxiosInstance);
    /**
     * Lists the account's email campaigns, newest first. The result is wrapped in
     * a `{ data, pagination }` envelope; pagination is page-token based.
     */
    getList(params?: ListEmailCampaignsParams): Promise<ListEmailCampaignsResponse>;
    /**
     * Create a new email campaign in the `draft` state. The campaign must
     * reference an existing sending domain via `domain_id` and include
     * a template `subject` within `template_attributes`.
     */
    create(params: CreateEmailCampaignParams): Promise<CreateEmailCampaignResponse>;
    /**
     * Get a single email campaign by ID.
     */
    get(id: number): Promise<GetEmailCampaignResponse>;
    /**
     * Update an existing `draft` email campaign. Only the provided attributes
     * are changed (PATCH semantics).
     */
    update(id: number, params: UpdateEmailCampaignParams): Promise<UpdateEmailCampaignResponse>;
    /**
     * Delete an email campaign by ID. Only a campaign in the `draft` state can be
     * deleted. Returns nothing (204 No Content).
     */
    delete(id: number): Promise<void>;
    /**
     * Start sending a `draft` campaign immediately. Runs full sending validation;
     * on failure the request fails with `422` and the campaign stays a `draft`.
     */
    start(id: number): Promise<EmailCampaignActionResponse>;
    /**
     * Schedule a `draft` campaign to start sending at a future time. After
     * scheduling, the time is reported back in
     * `current_state_metadata.scheduled_at`.
     */
    schedule(id: number, params: ScheduleEmailCampaignParams): Promise<EmailCampaignActionResponse>;
    /**
     * Cancel a `scheduled` campaign, removing the pending send job and returning
     * the campaign to the `draft` state.
     */
    cancel(id: number): Promise<EmailCampaignActionResponse>;
    /**
     * Terminate a campaign that is currently sending (`started`, `queued`, or
     * `paused`), aborting the in-flight send.
     */
    terminate(id: number): Promise<EmailCampaignActionResponse>;
    /**
     * Reset a `scheduled` campaign back to the `draft` state.
     */
    reset(id: number): Promise<EmailCampaignActionResponse>;
    /**
     * Get aggregated performance statistics for a single campaign. If the
     * campaign has never been started, all counts and rates are returned as `0`.
     * Use `start_date`/`end_date` (`YYYY-MM-DD`) to narrow the aggregation window.
     */
    getStats(id: number, params?: GetEmailCampaignStatsParams): Promise<GetEmailCampaignStatsResponse>;
}
//# sourceMappingURL=EmailCampaigns.d.ts.map