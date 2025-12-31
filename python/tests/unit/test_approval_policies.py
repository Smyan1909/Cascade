from storage.approval_policies import ApprovalPolicy, ApprovalPolicyMetadata, ApprovalRule


def test_approval_rule_exact_match():
    rule = ApprovalRule(
        rule_id="r1",
        capability_type="network",
        parameters={"host": "example.com"},
        decision="allow",
    )
    assert rule.matches("network", {"host": "example.com"}) is True
    assert rule.matches("network", {"host": "other.com"}) is False
    assert rule.matches("ui_action", {"host": "example.com"}) is False


def test_approval_rule_wildcard_param():
    rule = ApprovalRule(
        rule_id="r1",
        capability_type="ui_action",
        parameters={"platform": "WINDOWS", "action": "*"},
        decision="allow",
    )
    assert rule.matches("ui_action", {"platform": "WINDOWS", "action": "click_element"}) is True
    assert rule.matches("ui_action", {"platform": "JAVA", "action": "click_element"}) is False


def test_policy_decide_first_match():
    policy = ApprovalPolicy(
        metadata=ApprovalPolicyMetadata(policy_id="default", app_id="a", user_id="u"),
        rules=[
            ApprovalRule(rule_id="deny", capability_type="network", parameters={"host": "bad.com"}, decision="deny"),
            ApprovalRule(rule_id="allow", capability_type="network", parameters={"host": "good.com"}, decision="allow"),
        ],
    )
    assert policy.decide("network", {"host": "good.com"}) == "allow"
    assert policy.decide("network", {"host": "bad.com"}) == "deny"
    assert policy.decide("network", {"host": "unknown.com"}) is None


