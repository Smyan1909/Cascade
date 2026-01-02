import pytest


def test_selector_accepts_string_enum_names():
    from cascade_client.models import ControlType, PlatformSource, Selector

    sel = Selector.model_validate(
        {
            "platform_source": "WINDOWS",
            "name": "File",
            "control_type": "TAB",
            "path": [],
        }
    )

    assert sel.platform_source == PlatformSource.WINDOWS
    assert sel.control_type == ControlType.TAB


def test_selector_accepts_numeric_enum_values_and_numeric_strings():
    from cascade_client.models import ControlType, PlatformSource, Selector

    sel1 = Selector.model_validate(
        {
            "platform_source": 1,
            "control_type": 7,
            "path": [],
        }
    )
    assert sel1.platform_source == PlatformSource.WINDOWS
    assert sel1.control_type == ControlType.CUSTOM

    sel2 = Selector.model_validate(
        {
            "platform_source": "3",
            "control_type": "9",
            "path": [],
        }
    )
    assert sel2.platform_source == PlatformSource.WEB
    assert sel2.control_type == ControlType.TAB


def test_selector_rejects_unknown_enum_strings_with_helpful_message():
    from cascade_client.models import Selector

    with pytest.raises(ValueError) as e:
        Selector.model_validate(
            {
                "platform_source": "NOT_A_PLATFORM",
                "path": [],
            }
        )

    assert "Invalid PlatformSource" in str(e.value)


