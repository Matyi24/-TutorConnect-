"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const EmailCampaigns_1 = __importDefault(require("./resources/EmailCampaigns"));
class EmailCampaignsBaseAPI {
    constructor(client) {
        this.client = client;
        const emailCampaigns = new EmailCampaigns_1.default(this.client);
        this.getList = emailCampaigns.getList.bind(emailCampaigns);
        this.create = emailCampaigns.create.bind(emailCampaigns);
        this.get = emailCampaigns.get.bind(emailCampaigns);
        this.update = emailCampaigns.update.bind(emailCampaigns);
        this.delete = emailCampaigns.delete.bind(emailCampaigns);
        this.start = emailCampaigns.start.bind(emailCampaigns);
        this.schedule = emailCampaigns.schedule.bind(emailCampaigns);
        this.cancel = emailCampaigns.cancel.bind(emailCampaigns);
        this.terminate = emailCampaigns.terminate.bind(emailCampaigns);
        this.reset = emailCampaigns.reset.bind(emailCampaigns);
        this.getStats = emailCampaigns.getStats.bind(emailCampaigns);
    }
}
exports.default = EmailCampaignsBaseAPI;
