import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import { join } from 'path';

// Load the protobuf definition
const PROTO_PATH = join(__dirname, '..', 'proto', 'cascade.proto');

export class CascadeGrpcClient {
  private sessionClient: any;
  private automationClient: any;
  private visionClient: any;
  private isInitialized = false;

  constructor(private endpoint: string) {}

  async start(): Promise<void> {
    try {
      const packageDefinition = protoLoader.loadSync(PROTO_PATH, {
        keepCase: true,
        longs: String,
        enums: String,
        defaults: true,
        oneofs: true,
      });

      const protoDescriptor = grpc.loadPackageDefinition(packageDefinition);
      const cascade: any = protoDescriptor.cascade;

      // Ensure the endpoint is reachable (e.g. localhost:50051 or 172.x.x.x:50051)
      const credentials = grpc.credentials.createInsecure();
      const options = {
        'grpc.max_receive_message_length': 64 * 1024 * 1024, // 64MB for large screenshots
      };

      this.sessionClient = new cascade.SessionService(this.endpoint, credentials, options);
      this.automationClient = new cascade.AutomationService(this.endpoint, credentials, options);
      this.visionClient = new cascade.VisionService(this.endpoint, credentials, options);

      this.isInitialized = true;
    } catch (error) {
      console.error('Failed to initialize gRPC client:', error);
      throw error;
    }
  }

  isConnected(): boolean {
    return this.isInitialized;
  }

  stop(): void {
    if (this.sessionClient) this.sessionClient.close();
    if (this.automationClient) this.automationClient.close();
    if (this.visionClient) this.visionClient.close();
    this.isInitialized = false;
  }

  // --- SessionService ---
  async startApp(appName: string): Promise<any> {
    return new Promise((resolve, reject) => {
      this.sessionClient.StartApp({ app_name: appName }, (error: any, response: any) => {
        if (error) reject(error);
        else resolve(response);
      });
    });
  }

  async resetState(): Promise<any> {
    return new Promise((resolve, reject) => {
      this.sessionClient.ResetState({}, (error: any, response: any) => {
        if (error) reject(error);
        else resolve(response);
      });
    });
  }

  // --- AutomationService ---
  async performAction(
    actionType: string,
    selector: any,
    payload?: {
      text?: string;
      number?: number;
      json_payload?: string;
      text_entry_mode?: string;
    }
  ): Promise<any> {
    const action: any = {
      action_type: actionType,
      selector: selector
    };

    if (payload) {
      if (payload.text !== undefined) {
        action.text = payload.text;
      } else if (payload.number !== undefined) {
        action.number = payload.number;
      } else if (payload.json_payload !== undefined) {
        action.json_payload = payload.json_payload;
      }

      if (payload.text_entry_mode !== undefined) {
        action.text_entry_mode = payload.text_entry_mode;
      }
    }

    return new Promise((resolve, reject) => {
      this.automationClient.PerformAction(action, (error: any, response: any) => {
        if (error) reject(error);
        else resolve(response);
      });
    });
  }

  async getSemanticTree(): Promise<any> {
    return new Promise((resolve, reject) => {
      this.automationClient.GetSemanticTree({}, (error: any, response: any) => {
        if (error) reject(error);
        else resolve(response);
      });
    });
  }

  // --- VisionService ---
  async getScreenshot(): Promise<any> {
    return new Promise((resolve, reject) => {
      this.visionClient.GetMarkedScreenshot({}, (error: any, response: any) => {
        if (error) reject(error);
        else {
          // response.image is a Buffer from gRPC. We need to convert it to base64 for the plugin
          const b64Image = response.image ? response.image.toString('base64') : '';
          resolve({
            image: b64Image,
            format: response.format,
            marks: response.marks
          });
        }
      });
    });
  }
}