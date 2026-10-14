import { AxiosInstance } from "axios";
import EmailCampaignsApi from "./resources/EmailCampaigns";
export default class EmailCampaignsBaseAPI {
    private client;
    getList: EmailCampaignsApi["getList"];
    create: EmailCampaignsApi["create"];
    get: EmailCampaignsApi["get"];
    update: EmailCampaignsApi["update"];
    delete: EmailCampaignsApi["delete"];
    start: EmailCampaignsApi["start"];
    schedule: EmailCampaignsApi["schedule"];
    cancel: EmailCampaignsApi["cancel"];
    terminate: EmailCampaignsApi["terminate"];
    reset: EmailCampaignsApi["reset"];
    getStats: EmailCampaignsApi["getStats"];
    constructor(client: AxiosInstance);
}
//# sourceMappingURL=EmailCampaigns.d.ts.map