/**
 * Tests for Desktop Automation Tools
 */

import { registerDesktopTools } from './desktop-automation';
import { ToolRegistry } from './tool-registry';
import { MockCascadeGrpcClient } from '../test-utils';

describe('Desktop Automation Tools', () => {
  let registry: ToolRegistry;
  let mockClient: MockCascadeGrpcClient;

  beforeEach(() => {
    registry = new ToolRegistry();
    mockClient = new MockCascadeGrpcClient();
    registerDesktopTools(registry, async () => mockClient as any);
  });

  describe('cascade_click_element', () => {
    test('should be registered', () => {
      expect(registry.has('cascade_click_element')).toBe(true);
    });

    test('should click element by ID', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      const result = await registry.call('cascade_click_element', { platform_source: 'WINDOWS', id: 'button1' });

      expect(mockClient.performAction).toHaveBeenCalledWith('CLICK', { platform_source: 'WINDOWS', id: 'button1' });
      expect(result.content[0].text).toContain('success');
    });

    test('should handle click failure', async () => {
      mockClient.performAction.mockRejectedValue(new Error('Element not found'));
      const result = await registry.call('cascade_click_element', { platform_source: 'WINDOWS', id: 'button1' });

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('Element not found');
    });

    test('should require platform_source', async () => {
      const result = await registry.call('cascade_click_element', { id: 'button1' });
      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('platform_source is required');
    });
  });

  describe('cascade_type_text', () => {
    test('should type text into element', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      const result = await registry.call('cascade_type_text', {
        selector: { platform_source: 'WINDOWS', id: 'input1' },
        text: 'Hello World'
      });

      expect(mockClient.performAction).toHaveBeenCalledWith('TYPE_TEXT', { platform_source: 'WINDOWS', id: 'input1' }, {
        text: 'Hello World',
        text_entry_mode: 'APPEND'
      });
      expect(result.content[0].text).toContain('success');
    });
  });

  describe('cascade_get_semantic_tree', () => {
    test('should return semantic tree', async () => {
      mockClient.getSemanticTree.mockResolvedValue({ elements: [{ id: '1', name: 'Button' }, { id: '2', name: 'Input' }] });
      const result = await registry.call('cascade_get_semantic_tree', {});
      
      expect(mockClient.getSemanticTree).toHaveBeenCalled();
      expect(result.content[0].text).toContain('Button');
      expect(result.content[0].text).toContain('Input');
    });
  });

  describe('cascade_start_app', () => {
    test('should start application', async () => {
      mockClient.startApp.mockResolvedValue({ success: true });
      const result = await registry.call('cascade_start_app', { app_name: 'notepad' });

      expect(mockClient.startApp).toHaveBeenCalledWith('notepad');
      expect(result.content[0].text).toContain('success');
    });
  });

  describe('cascade_hover_element', () => {
    test('should hover over element', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      await registry.call('cascade_hover_element', { selector: { platform_source: 'WINDOWS', id: 'button1' } });
      expect(mockClient.performAction).toHaveBeenCalledWith('HOVER', { platform_source: 'WINDOWS', id: 'button1' });
    });
  });

  describe('cascade_focus_element', () => {
    test('should focus on element', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      await registry.call('cascade_focus_element', { selector: { platform_source: 'WINDOWS', id: 'input1' } });
      expect(mockClient.performAction).toHaveBeenCalledWith('FOCUS', { platform_source: 'WINDOWS', id: 'input1' });
    });
  });

  describe('cascade_scroll_element', () => {
    test('should scroll element', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      await registry.call('cascade_scroll_element', { selector: { platform_source: 'WINDOWS', id: 'scrollable' }, amount: 240 });
      expect(mockClient.performAction).toHaveBeenCalledWith('SCROLL', { platform_source: 'WINDOWS', id: 'scrollable' }, {
        number: 240
      });
    });
  });

  describe('cascade_wait_visible', () => {
    test('should wait for element visibility', async () => {
      mockClient.performAction.mockResolvedValue({ success: true });
      await registry.call('cascade_wait_visible', { selector: { platform_source: 'WINDOWS', id: 'loading' } });
      expect(mockClient.performAction).toHaveBeenCalledWith('WAIT_VISIBLE', { platform_source: 'WINDOWS', id: 'loading' });
    });
  });

  describe('extended actions', () => {
    test('should register advanced UIA tools', () => {
      expect(registry.has('cascade_toggle_element')).toBe(true);
      expect(registry.has('cascade_expand_element')).toBe(true);
      expect(registry.has('cascade_collapse_element')).toBe(true);
      expect(registry.has('cascade_select_element')).toBe(true);
      expect(registry.has('cascade_set_range_value')).toBe(true);
      expect(registry.has('cascade_send_keys')).toBe(true);
      expect(registry.has('cascade_window_minimize')).toBe(true);
      expect(registry.has('cascade_window_maximize')).toBe(true);
      expect(registry.has('cascade_window_restore')).toBe(true);
      expect(registry.has('cascade_window_close')).toBe(true);
      expect(registry.has('cascade_move_window')).toBe(true);
      expect(registry.has('cascade_resize_window')).toBe(true);
    });
  });
});
